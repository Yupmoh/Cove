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
        string nookId, ulong since, string controlToken, ILogger logger, CancellationToken ct)
    {
        PtyStreamClient? client = null;
        try
        {
            client = await PtyStreamClient.SubscribeAsync(
                dial,
                clientVersion,
                channel,
                nookId,
                since,
                controlToken,
                ct);
            ulong browserAcked = client.BaseOffset;
            var initialControl = client.AuthoritativeInitialResync
                ? $"{{\"t\":\"resync\",\"base\":{client.BaseOffset},\"modes\":\"{client.TerminalModePreambleBase64}\",\"checkpoint\":\"{client.TerminalCheckpointBase64}\",\"checkpointCols\":{client.CheckpointCols},\"checkpointRows\":{client.CheckpointRows}}}"
                : $"{{\"t\":\"base\",\"off\":{client.BaseOffset},\"head\":{client.ReplayUntilOffset},\"modes\":\"{client.TerminalModePreambleBase64}\",\"checkpoint\":\"{client.TerminalCheckpointBase64}\",\"checkpointCols\":{client.CheckpointCols},\"checkpointRows\":{client.CheckpointRows}}}";
            await SendText(ws, initialControl, ct);

            using var relayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var ackTask = ReceiveAcksAsync(ws, client, browserAcked, relayCts.Token);
            var pumpTask = client.PumpAsync(
                onData: async (data, c) => await SendData(ws, data.Offset, data.Data, c),
                onResync: async (resync, c) => await SendText(
                    ws,
                    $"{{\"t\":\"resync\",\"base\":{resync.BaseOffset},\"modes\":\"{Convert.ToBase64String(resync.TerminalModePreamble.Span)}\",\"checkpoint\":\"{Convert.ToBase64String(resync.TerminalCheckpoint.Span)}\",\"checkpointCols\":{resync.CheckpointCols},\"checkpointRows\":{resync.CheckpointRows}}}",
                    c),
                onEnd: async (completed, c) => await SendText(ws, $"{{\"t\":\"end\",\"code\":{completed.ExitCode}}}", c),
                relayCts.Token);
            var first = await Task.WhenAny(ackTask, pumpTask);
            await relayCts.CancelAsync();
            try
            {
                await Task.WhenAll(ackTask, pumpTask);
            }
            catch (OperationCanceledException) when (first.IsCompletedSuccessfully) { }
            await first;
        }
        catch (Exception ex) { logger.PtyWebSocketRelayFailed(nookId, ex.Message); }
        finally
        {
            if (client is not null) await client.DisposeAsync();
            if (ws.State == WebSocketState.Open)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }
    }

    private static async Task ReceiveAcksAsync(
        WebSocket ws, PtyStreamClient client, ulong browserAcked, CancellationToken ct)
    {
        var buffer = new byte[512];
        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
                return;
            var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var offset = ParseAck(text);
            if (offset > browserAcked)
            {
                browserAcked = offset;
                await client.AckAsync(offset, ct);
            }
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
