using System.Text;
using Xunit;

namespace Cove.Protocol.Tests;

public sealed class StreamPayloadTests
{
    [Fact]
    public void StreamData_RoundTrips()
    {
        byte[] raw = Encoding.ASCII.GetBytes("bye\r\n");
        byte[] dst = new byte[8 + raw.Length];
        int written = StreamPayload.WriteStreamData(dst, 4, raw);
        Assert.Equal(13, written);
        Assert.Equal(4UL, StreamPayload.ReadStreamDataOffset(dst));
        Assert.Equal("bye\r\n", Encoding.ASCII.GetString(StreamPayload.ReadStreamDataRaw(dst)));
    }

    [Fact]
    public void StreamData_EncodesCanonicalPayload()
    {
        ReadOnlySpan<byte> expected = ProtocolVectors.StreamDataPayload;
        byte[] dst = new byte[expected.Length];
        StreamPayload.WriteStreamData(dst, 4, Encoding.ASCII.GetBytes("bye\r\n"));
        Assert.True(expected.SequenceEqual(dst));
    }

    [Fact]
    public void Offset_RoundTrips()
    {
        byte[] dst = new byte[8];
        StreamPayload.WriteOffset(dst, 131072);
        Assert.Equal(131072UL, StreamPayload.ReadOffset(dst));
    }

    [Fact]
    public void StreamEnd_RoundTrips()
    {
        byte[] dst = new byte[12];
        int written = StreamPayload.WriteStreamEnd(dst, 9437700, 0);
        Assert.Equal(12, written);
        var (finalOffset, exitCode) = StreamPayload.ReadStreamEnd(dst);
        Assert.Equal(9437700UL, finalOffset);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Resync_ModernPayload_EncodesCanonicalLayoutAndRoundTrips()
    {
        ReadOnlySpan<byte> expected = ProtocolVectors.ModernResyncPayload;
        byte[] destination = new byte[expected.Length];

        int written = StreamPayload.WriteResync(
            destination,
            0x0102030405060708UL,
            132,
            43,
            "\x1b[?1006h"u8,
            [0x00, 0x41, 0xff, 0x1b, 0x5b, 0x48]);

        Assert.Equal(expected.Length, written);
        Assert.True(expected.SequenceEqual(destination));

        StreamResyncMessage message = StreamPayload.ReadResync(destination);
        Assert.Equal(0x0102030405060708UL, message.BaseOffset);
        Assert.Equal(132, message.CheckpointCols);
        Assert.Equal(43, message.CheckpointRows);
        Assert.True("\x1b[?1006h"u8.SequenceEqual(message.TerminalModePreamble.Span));
        Assert.True(new byte[] { 0x00, 0x41, 0xff, 0x1b, 0x5b, 0x48 }.AsSpan().SequenceEqual(message.TerminalCheckpoint.Span));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
    public void Resync_LegacyPayloadLengths_FallBackToOffsetAndModeBytes(int payloadLength)
    {
        ReadOnlySpan<byte> canonical = ProtocolVectors.LegacyResyncPayload;
        byte[] payload = canonical[..payloadLength].ToArray();

        StreamResyncMessage message = StreamPayload.ReadResync(payload);

        Assert.Equal(0x0102030405060708UL, message.BaseOffset);
        Assert.True(canonical[StreamPayload.OffsetSize..payloadLength].SequenceEqual(message.TerminalModePreamble.Span));
        Assert.True(message.TerminalCheckpoint.IsEmpty);
        Assert.Equal(0, message.CheckpointCols);
        Assert.Equal(0, message.CheckpointRows);
    }

    [Fact]
    public void Resync_ZeroModeLength_PreservesEntireBodyAsCheckpoint()
    {
        ReadOnlySpan<byte> expected = ProtocolVectors.ZeroModeResyncPayload;
        byte[] destination = new byte[expected.Length];

        int written = StreamPayload.WriteResync(
            destination,
            0x0102030405060708UL,
            80,
            24,
            ReadOnlySpan<byte>.Empty,
            [0x00, 0x7f, 0x80, 0xff]);

        Assert.Equal(expected.Length, written);
        Assert.True(expected.SequenceEqual(destination));

        StreamResyncMessage message = StreamPayload.ReadResync(destination);
        Assert.True(message.TerminalModePreamble.IsEmpty);
        Assert.True(new byte[] { 0x00, 0x7f, 0x80, 0xff }.AsSpan().SequenceEqual(message.TerminalCheckpoint.Span));
        Assert.Equal(80, message.CheckpointCols);
        Assert.Equal(24, message.CheckpointRows);
    }

    [Fact]
    public void Resync_ModeLengthAtBodyBoundary_PreservesEntireBodyAsModes()
    {
        ReadOnlySpan<byte> expected = ProtocolVectors.BoundaryModeResyncPayload;
        byte[] destination = new byte[expected.Length];

        int written = StreamPayload.WriteResync(
            destination,
            0x0102030405060708UL,
            200,
            60,
            [0x1b, 0x5b, 0x30, 0x6d, 0xff],
            ReadOnlySpan<byte>.Empty);

        Assert.Equal(expected.Length, written);
        Assert.True(expected.SequenceEqual(destination));

        StreamResyncMessage message = StreamPayload.ReadResync(destination);
        Assert.True(new byte[] { 0x1b, 0x5b, 0x30, 0x6d, 0xff }.AsSpan().SequenceEqual(message.TerminalModePreamble.Span));
        Assert.True(message.TerminalCheckpoint.IsEmpty);
        Assert.Equal(200, message.CheckpointCols);
        Assert.Equal(60, message.CheckpointRows);
    }

    [Fact]
    public void Resync_NegativeModeLength_IsRejected()
    {
        byte[] payload = ProtocolVectors.NegativeModeLengthResyncPayload.ToArray();

        Assert.Throws<InvalidDataException>(() => StreamPayload.ReadResync(payload));
    }

    [Fact]
    public void Resync_ModeLengthOnePastBody_IsRejected()
    {
        byte[] payload = ProtocolVectors.OnePastModeLengthResyncPayload.ToArray();

        Assert.Throws<InvalidDataException>(() => StreamPayload.ReadResync(payload));
    }
}
