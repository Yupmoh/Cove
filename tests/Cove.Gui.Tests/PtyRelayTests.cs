using System.Net.WebSockets;
using System.Text;
using Cove.Gui;
using Xunit;

public class PtyRelayTests
{
    [Fact]
    public async Task Relay_Forwards_Base_Data_Ack_Credit_End()
    {
        await using var engine = new FakeEngine();
        var tmp = Directory.CreateTempSubdirectory().FullName;
        await File.WriteAllTextAsync(Path.Combine(tmp, "index.html"), "<html>ok</html>");
        await using var server = new LoopbackServer(tmp, engine.Dial, "0.1.0", "dev", port: 0);
        server.Start();

        var serve = engine.ServeOnceAsync(baseOffset: 0, script: async s =>
        {
            await FakeEngine.WriteStreamData(s, 0, Encoding.ASCII.GetBytes("hi\r\n"));
            await Task.Delay(200);
            await FakeEngine.WriteEnd(s, 4, 0);
        });

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{server.Port}/pty?pane=p1&since=0"), CancellationToken.None);

        var baseMsg = await ReceiveText(ws);
        Assert.Contains("\"t\":\"base\"", baseMsg);

        var data = await ReceiveBinary(ws);
        Assert.Equal("hi\r\n", Encoding.ASCII.GetString(data));

        await ws.SendAsync(Encoding.UTF8.GetBytes("{\"t\":\"ack\",\"off\":4}"), WebSocketMessageType.Text, true, CancellationToken.None);

        var endMsg = await ReceiveText(ws);
        Assert.Contains("\"t\":\"end\"", endMsg);

        await serve;
        Assert.Contains(4UL, engine.Credits);
    }

    [Fact]
    public async Task Relay_Forwards_Resync_As_Text_And_Resets_Base()
    {
        await using var engine = new FakeEngine();
        var tmp = Directory.CreateTempSubdirectory().FullName;
        await File.WriteAllTextAsync(Path.Combine(tmp, "index.html"), "<html>ok</html>");
        await using var server = new LoopbackServer(tmp, engine.Dial, "0.1.0", "dev", port: 0);
        server.Start();

        var serve = engine.ServeOnceAsync(baseOffset: 0, script: async s =>
        {
            await FakeEngine.WriteResync(s, 9437184);
            await Task.Delay(100);
            await FakeEngine.WriteEnd(s, 9437184, 0);
        });

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{server.Port}/pty?pane=p1&since=0"), CancellationToken.None);
        _ = await ReceiveText(ws);
        var resync = await ReceiveText(ws);
        Assert.Contains("\"t\":\"resync\"", resync);
        Assert.Contains("9437184", resync);
        await serve;
    }

    private static async Task<string> ReceiveText(ClientWebSocket ws)
    {
        var buf = new byte[4096];
        while (true)
        {
            var r = await ws.ReceiveAsync(buf, CancellationToken.None);
            if (r.MessageType == WebSocketMessageType.Text) return Encoding.UTF8.GetString(buf, 0, r.Count);
        }
    }
    private static async Task<byte[]> ReceiveBinary(ClientWebSocket ws)
    {
        var buf = new byte[4096];
        while (true)
        {
            var r = await ws.ReceiveAsync(buf, CancellationToken.None);
            if (r.MessageType == WebSocketMessageType.Binary) return buf[..r.Count];
        }
    }
}
