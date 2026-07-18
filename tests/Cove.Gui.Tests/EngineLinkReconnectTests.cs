using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Cove.Protocol;
using Xunit;

namespace Cove.Gui.Tests;

public sealed class EngineLinkReconnectTests
{
    private static async Task ServeOneConnectionAsync(TcpListener listener, CancellationToken ct)
    {
        using var client = await listener.AcceptTcpClientAsync(ct);
        await using var conn = new FrameConnection(client.GetStream());

        var helloFrame = (await conn.ReadFrameAsync(ct))!.Value;
        var helloRequest = ControlCodec.DecodeRequest(helloFrame.Payload);
        var helloData = JsonSerializer.SerializeToElement(
            new HelloResult(ProtocolConstants.SemanticProtocolVersion, "0.4.0", 1, "dev"),
            CoveJsonContext.Default.HelloResult);
        await conn.WriteFrameAsync(FrameType.Response, 0,
            ControlCodec.Encode(new ControlResponse(helloRequest.Id, true, helloData)), ct);

        var pingFrame = (await conn.ReadFrameAsync(ct))!.Value;
        var pingRequest = ControlCodec.DecodeRequest(pingFrame.Payload);
        await conn.WriteFrameAsync(FrameType.Response, 0,
            ControlCodec.Encode(new ControlResponse(pingRequest.Id, true, null)), ct);
    }

    [Fact]
    public async Task Reconnect_EmitsEngineReconnectedOnlyOnSecondConnect()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cancellation.Token;
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;

        var events = new ConcurrentBag<string>();

        await using var link = new EngineLink(
            async token =>
            {
                var client = new TcpClient();
                await client.ConnectAsync(endpoint.Address, endpoint.Port, token);
                return client.GetStream();
            },
            "0.4.0",
            "dev");
        link.SetEngineEventHandler((channel, _) => events.Add(channel));

        try
        {
            var firstServe = ServeOneConnectionAsync(listener, ct);
            var firstPing = await link.RequestAsync("cove://sys/ping", null, ct);
            Assert.True(firstPing.Ok);
            await firstServe;
            Assert.DoesNotContain("engine.reconnected", events);

            var secondServe = ServeOneConnectionAsync(listener, ct);
            ControlResponse? secondPing = null;
            Exception? lastError = null;
            for (var attempt = 0; attempt < 100 && secondPing is null; attempt++)
            {
                try { secondPing = await link.RequestAsync("cove://sys/ping", null, ct); }
                catch (Exception ex) when (ex is IOException or InvalidOperationException or OperationCanceledException)
                {
                    lastError = ex;
                    await Task.Delay(50, ct);
                }
            }
            await secondServe;

            Assert.True(secondPing is not null, $"reconnect ping never succeeded; last transient error: {lastError}");
            Assert.True(secondPing!.Ok);
            Assert.Equal(1, events.Count(c => c == "engine.reconnected"));
        }
        finally
        {
            listener.Stop();
        }
    }
}
