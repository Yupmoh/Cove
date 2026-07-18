using System.Buffers.Binary;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Gui;

public static class PtyWsHandler
{
    public static async Task RunAsync(
        WebSocket ws, Func<CancellationToken, Task<Stream>> dial, string clientVersion, string channel,
        string nookId, ulong since, ILogger logger, CancellationToken ct)
    {
        PtyStreamClient? client = null;
        try
        {
            client = await PtyStreamClient.SubscribeAsync(dial, clientVersion, channel, nookId, since, ct);
            ulong browserAcked = client.BaseOffset;
            await SendText(ws, $"{{\"t\":\"base\",\"off\":{client.BaseOffset},\"head\":{client.ReplayUntilOffset},\"modes\":\"{client.TerminalModePreambleBase64}\",\"checkpoint\":\"{client.TerminalCheckpointBase64}\",\"checkpointCols\":{client.CheckpointCols},\"checkpointRows\":{client.CheckpointRows}}}", ct);

            var ackTask = Task.Run(async () =>
            {
                var buf = new byte[512];
                while (ws.State == WebSocketState.Open)
                {
                    var r = await ws.ReceiveAsync(buf, ct);
                    if (r.MessageType == WebSocketMessageType.Close) break;
                    var text = Encoding.UTF8.GetString(buf, 0, r.Count);
                    var off = ParseAck(text);
                    if (off > browserAcked) { browserAcked = off; await client!.AckAsync(off, ct); }
                }
            }, ct);

            await client.PumpAsync(
                onData: async (offset, raw, c) => await SendData(ws, offset, raw, c),
                onResync: async (newBase, modes, checkpoint, cols, rows, c) => await SendText(ws, $"{{\"t\":\"resync\",\"base\":{newBase},\"modes\":\"{modes}\",\"checkpoint\":\"{checkpoint}\",\"checkpointCols\":{cols},\"checkpointRows\":{rows}}}", c),
                onEnd: async (final, code, c) => await SendText(ws, $"{{\"t\":\"end\",\"code\":{code}}}", c),
                ct);
        }
        catch (Exception ex) { logger.PtyWebSocketRelayFailed(nookId, ex.Message); }
        finally
        {
            if (client is not null) await client.DisposeAsync();
            if (ws.State == WebSocketState.Open)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }
    }

    private static Task SendText(WebSocket ws, string s, CancellationToken ct)
        => ws.SendAsync(Encoding.UTF8.GetBytes(s), WebSocketMessageType.Text, true, ct);
    private static async Task SendData(WebSocket ws, ulong offset, ReadOnlyMemory<byte> raw, CancellationToken ct)
    {
        var header = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(header, offset);
        await ws.SendAsync(header, WebSocketMessageType.Binary, false, ct);
        await ws.SendAsync(raw, WebSocketMessageType.Binary, true, ct);
    }

    private static ulong ParseAck(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("t", out var t) && t.GetString() == "ack" && root.TryGetProperty("off", out var off))
                return off.GetUInt64();
        }
        catch (JsonException) { return 0; }
        return 0;
    }
}
