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
    public void StreamData_MatchesExampleB_Frame3Payload()
    {
        byte[] expected = HexUtil.Bytes("04 00 00 00 00 00 00 00 62 79 65 0d 0a");
        byte[] dst = new byte[expected.Length];
        StreamPayload.WriteStreamData(dst, 4, Encoding.ASCII.GetBytes("bye\r\n"));
        Assert.Equal(expected, dst);
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
}
