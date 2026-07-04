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
        Span<byte> dst = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(dst, header);
        Assert.True(FrameHeader.TryRead(dst, out FrameHeader read, out string? err));
        Assert.Null(err);
        Assert.Equal(header, read);
    }

    [Fact]
    public void ExampleA_Request_Decodes()
    {
        byte[] frame = HexUtil.Bytes(
            "43 4f 56 45 01 01 00 00 00 00 00 00 00 00 00 00 01 00 00 00 2c 00 00 00" +
            "7b 22 69 64 22 3a 22 31 22 2c 22 75 72 69 22 3a 22 63 6f 76 65 3a 2f 2f" +
            "63 6f 6d 6d 61 6e 64 73 2f 70 61 6e 65 2e 6c 69 73 74 22 7d");
        Assert.True(FrameHeader.TryRead(frame, out FrameHeader h, out _));
        Assert.Equal(FrameType.Request, h.Type);
        Assert.Equal(0UL, h.StreamId);
        Assert.Equal(1u, h.Seq);
        Assert.Equal(44u, h.Length);
        ControlRequest req = ControlCodec.DecodeRequest(frame.AsSpan(ProtocolConstants.HeaderSize));
        Assert.Equal("1", req.Id);
        Assert.Equal("cove://commands/pane.list", req.Uri);
        Assert.Null(req.Params);
    }

    [Fact]
    public void ExampleA_Response_Decodes()
    {
        byte[] frame = HexUtil.Bytes(
            "43 4f 56 45 01 02 00 00 00 00 00 00 00 00 00 00 01 00 00 00 28 00 00 00" +
            "7b 22 69 64 22 3a 22 31 22 2c 22 6f 6b 22 3a 74 72 75 65 2c 22 64 61 74" +
            "61 22 3a 7b 22 70 61 6e 65 73 22 3a 5b 5d 7d 7d");
        Assert.True(FrameHeader.TryRead(frame, out FrameHeader h, out _));
        Assert.Equal(FrameType.Response, h.Type);
        Assert.Equal(40u, h.Length);
        ControlResponse resp = ControlCodec.DecodeResponse(frame.AsSpan(ProtocolConstants.HeaderSize));
        Assert.Equal("1", resp.Id);
        Assert.True(resp.Ok);
        Assert.Null(resp.Error);
    }

    [Fact]
    public void ExampleB_StreamData_DecodesOffsetAndRaw()
    {
        byte[] f3 = HexUtil.Bytes(
            "43 4f 56 45 01 05 00 00 01 00 00 00 00 00 00 00 0c 00 00 00 0d 00 00 00" +
            "04 00 00 00 00 00 00 00 62 79 65 0d 0a");
        Assert.True(FrameHeader.TryRead(f3, out FrameHeader h, out _));
        Assert.Equal(FrameType.StreamData, h.Type);
        Assert.Equal(1UL, h.StreamId);
        Assert.Equal(13u, h.Length);
        var payload = f3.AsSpan(ProtocolConstants.HeaderSize);
        Assert.Equal(4UL, StreamPayload.ReadStreamDataOffset(payload));
        Assert.Equal("bye\r\n", Encoding.ASCII.GetString(StreamPayload.ReadStreamDataRaw(payload)));
    }

    [Fact]
    public void ExampleC_Credit_Resync_StreamEnd_Decode()
    {
        byte[] credit = HexUtil.Bytes(
            "43 4f 56 45 01 06 00 00 01 00 00 00 00 00 00 00 14 00 00 00 08 00 00 00" +
            "00 00 02 00 00 00 00 00");
        Assert.True(FrameHeader.TryRead(credit, out FrameHeader ch, out _));
        Assert.Equal(FrameType.Credit, ch.Type);
        Assert.Equal(131072UL, StreamPayload.ReadOffset(credit.AsSpan(ProtocolConstants.HeaderSize)));

        byte[] resync = HexUtil.Bytes(
            "43 4f 56 45 01 07 00 00 01 00 00 00 00 00 00 00 90 01 00 00 08 00 00 00" +
            "00 00 90 00 00 00 00 00");
        Assert.True(FrameHeader.TryRead(resync, out FrameHeader rh, out _));
        Assert.Equal(FrameType.Resync, rh.Type);
        Assert.Equal(9437184UL, StreamPayload.ReadOffset(resync.AsSpan(ProtocolConstants.HeaderSize)));

        byte[] end = HexUtil.Bytes(
            "43 4f 56 45 01 08 00 00 01 00 00 00 00 00 00 00 91 01 00 00 0c 00 00 00" +
            "04 02 90 00 00 00 00 00 00 00 00 00");
        Assert.True(FrameHeader.TryRead(end, out FrameHeader eh, out _));
        Assert.Equal(FrameType.StreamEnd, eh.Type);
        var (finalOffset, exitCode) = StreamPayload.ReadStreamEnd(end.AsSpan(ProtocolConstants.HeaderSize));
        Assert.Equal(9437700UL, finalOffset);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Reject_BadMagic()
    {
        byte[] b = new byte[ProtocolConstants.HeaderSize];
        b[0] = (byte)'X';
        Assert.False(FrameHeader.TryRead(b, out _, out string? err));
        Assert.Equal("malformed_frame", err);
    }

    [Fact]
    public void Reject_BadWireVersion()
    {
        Span<byte> b = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(b, new FrameHeader(FrameType.Request, 0, 1, 0));
        b[4] = 2;
        Assert.False(FrameHeader.TryRead(b, out _, out string? err));
        Assert.Equal("unsupported_version", err);
    }

    [Fact]
    public void Reject_TypeZeroAndUnknownType()
    {
        Span<byte> zero = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(zero, new FrameHeader(FrameType.Request, 0, 1, 0));
        zero[5] = 0;
        Assert.False(FrameHeader.TryRead(zero, out _, out string? e0));
        Assert.Equal("unknown_frame_type", e0);

        Span<byte> unk = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(unk, new FrameHeader(FrameType.Request, 0, 1, 0));
        unk[5] = 9;
        Assert.False(FrameHeader.TryRead(unk, out _, out string? e9));
        Assert.Equal("unknown_frame_type", e9);
    }

    [Fact]
    public void Reject_NonZeroFlagsOrReserved()
    {
        Span<byte> flags = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(flags, new FrameHeader(FrameType.Request, 0, 1, 0));
        flags[6] = 1;
        Assert.False(FrameHeader.TryRead(flags, out _, out string? ef));
        Assert.Equal("malformed_frame", ef);
    }

    [Fact]
    public void Reject_StreamIdRuleViolations()
    {
        Span<byte> ctrl = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(ctrl, new FrameHeader(FrameType.Request, 0, 1, 0));
        ctrl[8] = 5;
        Assert.False(FrameHeader.TryRead(ctrl, out _, out string? ec));
        Assert.Equal("malformed_frame", ec);

        Span<byte> data = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(data, new FrameHeader(FrameType.StreamData, 1, 1, 0));
        for (int i = 8; i < 16; i++) data[i] = 0;
        Assert.False(FrameHeader.TryRead(data, out _, out string? ed));
        Assert.Equal("malformed_frame", ed);
    }

    [Fact]
    public void Reject_OversizeLength()
    {
        Span<byte> b = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(b, new FrameHeader(FrameType.Request, 0, 1, (uint)ProtocolConstants.MaxFramePayload + 1));
        Assert.False(FrameHeader.TryRead(b, out _, out string? err));
        Assert.Equal("frame_too_large", err);
    }

    [Fact]
    public void ShortHeader_NeedsMoreBytes()
    {
        byte[] b = new byte[10];
        Assert.False(FrameHeader.TryRead(b, out _, out string? err));
        Assert.Equal("short_header", err);
    }
}
