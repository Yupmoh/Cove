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

        var serve = engine.ServeOnceAsync(baseOffset: 0, replayUntilOffset: 4, script: async s =>
        {
            await FakeEngine.WriteStreamData(s, 0, Encoding.ASCII.GetBytes("hi\r\n"));
            await Task.Delay(200);
            await FakeEngine.WriteEnd(s, 4, 0);
        });

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{server.Port}/pty?nook=p1&since=0"), CancellationToken.None);

        var baseMsg = await ReceiveText(ws);
        Assert.Contains("\"t\":\"base\"", baseMsg);
        Assert.Contains("\"head\":4", baseMsg);

        var data = await ReceiveBinary(ws);
        Assert.Equal(12, data.Length);
        Assert.Equal(0UL, System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(data));
        Assert.Equal("hi\r\n", Encoding.ASCII.GetString(data, 8, 4));

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

        var serve = engine.ServeOnceAsync(baseOffset: 0, replayUntilOffset: 0, script: async s =>
        {
            await FakeEngine.WriteResync(s, 9437184);
            await Task.Delay(100);
            await FakeEngine.WriteEnd(s, 9437184, 0);
        });

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{server.Port}/pty?nook=p1&since=0"), CancellationToken.None);
        _ = await ReceiveText(ws);
        var resync = await ReceiveText(ws);
        Assert.Contains("\"t\":\"resync\"", resync);
        Assert.Contains("9437184", resync);
        await serve;
    }

    [Fact]
    public async Task Relay_Streams_Four_Terminals_Concurrently_Without_CrossTalk()
    {
        await using var engine = new FakeEngine();
        var tmp = Directory.CreateTempSubdirectory().FullName;
        await File.WriteAllTextAsync(Path.Combine(tmp, "index.html"), "<html>ok</html>");
        await using var server = new LoopbackServer(tmp, engine.Dial, "0.1.0", "dev", port: 0);
        server.Start();

        var expected = Enumerable.Range(0, 4).Select(i => $"terminal-{i}\r\n").ToArray();
        var serves = expected.Select(payload => engine.ServeOnceAsync(0, (ulong)payload.Length, async stream =>
        {
            await FakeEngine.WriteStreamData(stream, 0, Encoding.ASCII.GetBytes(payload));
            await Task.Delay(100);
            await FakeEngine.WriteEnd(stream, (ulong)payload.Length, 0);
        })).ToArray();
        var sockets = new List<ClientWebSocket>();
        try
        {
            for (var i = 0; i < expected.Length; i++)
            {
                var ws = new ClientWebSocket();
                sockets.Add(ws);
                await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{server.Port}/pty?nook=p{i}&since=0"), CancellationToken.None);
                _ = await ReceiveText(ws);
            }
            var received = new List<string>();
            foreach (var ws in sockets)
            {
                var frame = await ReceiveBinary(ws);
                Assert.Equal(0UL, System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(frame));
                received.Add(Encoding.ASCII.GetString(frame, 8, frame.Length - 8));
            }
            Assert.Equal(expected.Order(), received.Order());
            await Task.WhenAll(serves);
        }
        finally
        {
            foreach (var ws in sockets) ws.Dispose();
        }
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
        using var message = new MemoryStream();
        while (true)
        {
            var r = await ws.ReceiveAsync(buf, CancellationToken.None);
            if (r.MessageType != WebSocketMessageType.Binary) continue;
            message.Write(buf, 0, r.Count);
            if (r.EndOfMessage) return message.ToArray();
        }
    }
}
