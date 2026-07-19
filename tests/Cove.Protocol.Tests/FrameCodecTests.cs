using System.Text;
using Xunit;

namespace Cove.Protocol.Tests;

public sealed class FrameCodecTests
{
    [Theory]
    [InlineData(FrameType.Request, 0UL)]
    [InlineData(FrameType.Response, 0UL)]
    [InlineData(FrameType.Event, 0UL)]
    [InlineData(FrameType.Error, 0UL)]
    [InlineData(FrameType.StreamData, 1UL)]
    [InlineData(FrameType.Credit, 2UL)]
    [InlineData(FrameType.Resync, 3UL)]
    [InlineData(FrameType.StreamEnd, 7UL)]
    public void Header_RoundTrips(FrameType type, ulong streamId)
    {
        var header = new FrameHeader(type, streamId, 42, 100);
        Span<byte> destination = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(destination, header);
        Assert.True(FrameHeader.TryRead(destination, out FrameHeader read, out string? error));
        Assert.Null(error);
        Assert.Equal(header, read);
    }

    [Fact]
    public void NookListRequestFrame_EncodesAndDecodesCanonicalVector()
    {
        ReadOnlySpan<byte> frame = ProtocolVectors.NookListRequestFrame;
        FrameHeader header = ReadHeader(frame);
        Assert.Equal(new FrameHeader(FrameType.Request, 0, 1, 44), header);
        AssertHeaderEncodes(frame, header);

        ControlRequest request = ControlCodec.DecodeRequest(frame[ProtocolConstants.HeaderSize..]);
        Assert.Equal("1", request.Id);
        Assert.Equal("cove://commands/nook.list", request.Uri);
        Assert.Null(request.Params);
        Assert.True(frame[ProtocolConstants.HeaderSize..].SequenceEqual(ControlCodec.Encode(request)));
    }

    [Fact]
    public void NookListResponseFrame_EncodesAndDecodesCanonicalVector()
    {
        ReadOnlySpan<byte> frame = ProtocolVectors.NookListResponseFrame;
        FrameHeader header = ReadHeader(frame);
        Assert.Equal(new FrameHeader(FrameType.Response, 0, 1, 40), header);
        AssertHeaderEncodes(frame, header);

        ControlResponse response = ControlCodec.DecodeResponse(frame[ProtocolConstants.HeaderSize..]);
        Assert.Equal("1", response.Id);
        Assert.True(response.Ok);
        Assert.Equal(0, response.Data!.Value.GetProperty("nooks").GetArrayLength());
        Assert.Null(response.Error);
        Assert.True(frame[ProtocolConstants.HeaderSize..].SequenceEqual(ControlCodec.Encode(response)));
    }

    [Fact]
    public void StreamDataFrame_EncodesAndDecodesCanonicalVector()
    {
        ReadOnlySpan<byte> frame = ProtocolVectors.StreamDataFrame;
        FrameHeader header = ReadHeader(frame);
        Assert.Equal(new FrameHeader(FrameType.StreamData, 1, 12, 13), header);
        AssertHeaderEncodes(frame, header);

        ReadOnlySpan<byte> payload = frame[ProtocolConstants.HeaderSize..];
        Assert.Equal(4UL, StreamPayload.ReadStreamDataOffset(payload));
        Assert.Equal("bye\r\n", Encoding.ASCII.GetString(StreamPayload.ReadStreamDataRaw(payload)));

        Span<byte> encoded = stackalloc byte[13];
        Assert.Equal(13, StreamPayload.WriteStreamData(encoded, 4, "bye\r\n"u8));
        Assert.True(payload.SequenceEqual(encoded));
    }

    [Fact]
    public void CreditAndResyncFrames_EncodeAndDecodeCanonicalVectors()
    {
        AssertOffsetFrame(ProtocolVectors.CreditFrame, FrameType.Credit, 20, 131072);
        AssertOffsetFrame(ProtocolVectors.ResyncFrame, FrameType.Resync, 400, 9437184);
    }

    [Fact]
    public void StreamEndFrame_EncodesAndDecodesCanonicalVector()
    {
        ReadOnlySpan<byte> frame = ProtocolVectors.StreamEndFrame;
        FrameHeader header = ReadHeader(frame);
        Assert.Equal(new FrameHeader(FrameType.StreamEnd, 1, 401, 12), header);
        AssertHeaderEncodes(frame, header);

        ReadOnlySpan<byte> payload = frame[ProtocolConstants.HeaderSize..];
        var (finalOffset, exitCode) = StreamPayload.ReadStreamEnd(payload);
        Assert.Equal(9437700UL, finalOffset);
        Assert.Equal(0, exitCode);

        Span<byte> encoded = stackalloc byte[12];
        Assert.Equal(12, StreamPayload.WriteStreamEnd(encoded, finalOffset, exitCode));
        Assert.True(payload.SequenceEqual(encoded));
    }

    [Fact]
    public void Reject_BadMagic()
    {
        byte[] bytes = new byte[ProtocolConstants.HeaderSize];
        bytes[0] = (byte)'X';
        Assert.False(FrameHeader.TryRead(bytes, out _, out string? error));
        Assert.Equal("malformed_frame", error);
    }

    [Fact]
    public void Reject_BadWireVersion()
    {
        Span<byte> bytes = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(bytes, new FrameHeader(FrameType.Request, 0, 1, 0));
        bytes[4] = 2;
        Assert.False(FrameHeader.TryRead(bytes, out _, out string? error));
        Assert.Equal("unsupported_version", error);
    }

    [Fact]
    public void Reject_TypeZeroAndUnknownType()
    {
        Span<byte> zero = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(zero, new FrameHeader(FrameType.Request, 0, 1, 0));
        zero[5] = 0;
        Assert.False(FrameHeader.TryRead(zero, out _, out string? zeroError));
        Assert.Equal("unknown_frame_type", zeroError);

        Span<byte> unknown = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(unknown, new FrameHeader(FrameType.Request, 0, 1, 0));
        unknown[5] = 9;
        Assert.False(FrameHeader.TryRead(unknown, out _, out string? unknownError));
        Assert.Equal("unknown_frame_type", unknownError);
    }

    [Fact]
    public void Reject_NonZeroFlagsOrReserved()
    {
        Span<byte> bytes = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(bytes, new FrameHeader(FrameType.Request, 0, 1, 0));
        bytes[6] = 1;
        Assert.False(FrameHeader.TryRead(bytes, out _, out string? error));
        Assert.Equal("malformed_frame", error);
    }

    [Fact]
    public void Reject_StreamIdRuleViolations()
    {
        Span<byte> control = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(control, new FrameHeader(FrameType.Request, 0, 1, 0));
        control[8] = 5;
        Assert.False(FrameHeader.TryRead(control, out _, out string? controlError));
        Assert.Equal("malformed_frame", controlError);

        Span<byte> data = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(data, new FrameHeader(FrameType.StreamData, 1, 1, 0));
        data[8..16].Clear();
        Assert.False(FrameHeader.TryRead(data, out _, out string? dataError));
        Assert.Equal("malformed_frame", dataError);
    }

    [Fact]
    public void Reject_OversizeLength()
    {
        Span<byte> bytes = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(bytes, new FrameHeader(FrameType.Request, 0, 1, (uint)ProtocolConstants.MaxFramePayload + 1));
        Assert.False(FrameHeader.TryRead(bytes, out _, out string? error));
        Assert.Equal("frame_too_large", error);
    }

    [Fact]
    public void ShortHeader_NeedsMoreBytes()
    {
        byte[] bytes = new byte[10];
        Assert.False(FrameHeader.TryRead(bytes, out _, out string? error));
        Assert.Equal("short_header", error);
    }

    private static FrameHeader ReadHeader(ReadOnlySpan<byte> frame)
    {
        Assert.True(FrameHeader.TryRead(frame, out FrameHeader header, out string? error));
        Assert.Null(error);
        return header;
    }

    private static void AssertHeaderEncodes(ReadOnlySpan<byte> frame, FrameHeader header)
    {
        Span<byte> encoded = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(encoded, header);
        Assert.True(frame[..ProtocolConstants.HeaderSize].SequenceEqual(encoded));
    }

    private static void AssertOffsetFrame(
        ReadOnlySpan<byte> frame,
        FrameType type,
        uint sequence,
        ulong expectedOffset)
    {
        FrameHeader header = ReadHeader(frame);
        Assert.Equal(new FrameHeader(type, 1, sequence, 8), header);
        AssertHeaderEncodes(frame, header);

        ReadOnlySpan<byte> payload = frame[ProtocolConstants.HeaderSize..];
        Assert.Equal(expectedOffset, StreamPayload.ReadOffset(payload));
        Span<byte> encoded = stackalloc byte[8];
        StreamPayload.WriteOffset(encoded, expectedOffset);
        Assert.True(payload.SequenceEqual(encoded));
    }
}
