using Cove.Gui;
using Xunit;

public class WebSocketHandshakeTests
{
    [Fact]
    public void Rfc6455_AcceptKey_MatchesCanonicalVector()
    {
        Assert.Equal("s3pPLMBiTxaQ9kYGzzhZRbK+xOo=", LoopbackServer.ComputeAcceptKey("dGhlIHNhbXBsZSBub25jZQ=="));
    }
}
