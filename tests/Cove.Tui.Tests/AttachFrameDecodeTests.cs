using System.Buffers.Binary;
using Cove.Protocol;
using Cove.Tui.Attach;
using Xunit;

namespace Cove.Tui.Tests;

public sealed class AttachFrameDecodeTests
{
    private static byte[] U64(ulong v) { var b = new byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(b, v); return b; }
    private static byte[] StreamDataPayload(ulong offset, byte[] raw)
    {
        var b = new byte[8 + raw.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(0, 8), offset);
        Array.Copy(raw, 0, b, 8, raw.Length);
        return b;
    }
    private static byte[] StreamEndPayload(ulong finalOffset, int exitCode)
    {
        var b = new byte[12];
        BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(0, 8), finalOffset);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(8, 4), exitCode);
        return b;
    }

    [Fact]
    public void StreamDataOffset_ReadsFirst8Bytes()
    {
        var payload = StreamDataPayload(42, new byte[] { 1, 2, 3 });
        Assert.Equal(42UL, AttachFrameDecode.StreamDataOffset(payload));
    }

    [Fact]
    public void StreamDataRaw_ReturnsBytesAfterOffset()
    {
        var payload = StreamDataPayload(0, new byte[] { 0x41, 0x42, 0x43 });
        var raw = AttachFrameDecode.StreamDataRaw(payload);
        Assert.Equal(new byte[] { 0x41, 0x42, 0x43 }, raw.ToArray());
    }

    [Fact]
    public void StreamDataRaw_EmptyPayload_ReturnsEmpty()
    {
        var payload = StreamDataPayload(0, Array.Empty<byte>());
        var raw = AttachFrameDecode.StreamDataRaw(payload);
        Assert.Equal(0, raw.Length);
    }

    [Fact]
    public void ResyncOffset_ReadsFirst8Bytes()
    {
        var payload = U64(99);
        Assert.Equal(99UL, AttachFrameDecode.ResyncOffset(payload));
    }

    [Fact]
    public void StreamEndFields_ReadsOffsetAndExitCode()
    {
        var payload = StreamEndPayload(1024UL, 0);
        var (finalOffset, exitCode) = AttachFrameDecode.StreamEndFields(payload);
        Assert.Equal(1024UL, finalOffset);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void StreamEndFields_ReadsNonZeroExitCode()
    {
        var payload = StreamEndPayload(500UL, 42);
        var (finalOffset, exitCode) = AttachFrameDecode.StreamEndFields(payload);
        Assert.Equal(500UL, finalOffset);
        Assert.Equal(42, exitCode);
    }

    [Fact]
    public void NextAckOffset_AdvancesByDataLength()
    {
        Assert.Equal(150UL, AttachFrameDecode.NextAckOffset(100, 50, 100));
    }

    [Fact]
    public void NextAckOffset_DoesNotGoBackwards()
    {
        Assert.Equal(200UL, AttachFrameDecode.NextAckOffset(200, 50, 50));
    }

    [Fact]
    public void NextAckOffset_ZeroData_ReturnsCurrent()
    {
        Assert.Equal(100UL, AttachFrameDecode.NextAckOffset(100, 100, 0));
    }

    [Fact]
    public void EncodeCredit_RoundTrips()
    {
        var encoded = AttachFrameDecode.EncodeCredit(12345);
        Assert.Equal(12345UL, BinaryPrimitives.ReadUInt64LittleEndian(encoded));
    }

    [Fact]
    public void IsStreamFrame_TrueForStreamTypes()
    {
        Assert.True(AttachFrameDecode.IsStreamFrame(FrameType.StreamData));
        Assert.True(AttachFrameDecode.IsStreamFrame(FrameType.Resync));
        Assert.True(AttachFrameDecode.IsStreamFrame(FrameType.StreamEnd));
    }

    [Fact]
    public void IsStreamFrame_FalseForControlTypes()
    {
        Assert.False(AttachFrameDecode.IsStreamFrame(FrameType.Request));
        Assert.False(AttachFrameDecode.IsStreamFrame(FrameType.Response));
        Assert.False(AttachFrameDecode.IsStreamFrame(FrameType.Credit));
    }

    [Fact]
    public void BelongsToStream_TrueWhenMatchingStreamIdAndStreamType()
    {
        Assert.True(AttachFrameDecode.BelongsToStream(5, 5, FrameType.StreamData));
    }

    [Fact]
    public void BelongsToStream_FalseWhenStreamIdMismatch()
    {
        Assert.False(AttachFrameDecode.BelongsToStream(5, 9, FrameType.StreamData));
    }

    [Fact]
    public void BelongsToStream_FalseWhenNotStreamType()
    {
        Assert.False(AttachFrameDecode.BelongsToStream(5, 5, FrameType.Request));
    }
}
