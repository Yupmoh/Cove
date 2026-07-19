using System.Text.Json;
using Cove.Protocol;

namespace Cove.Gui;

public sealed class PtyStreamClient : IAsyncDisposable
{
    private readonly Stream _s;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private uint _seq;
    public ulong StreamId { get; }
    public ulong BaseOffset { get; }
    public ulong ReplayUntilOffset { get; }
    public string TerminalModePreambleBase64 { get; }
    public string TerminalCheckpointBase64 { get; }
    public int CheckpointCols { get; }
    public int CheckpointRows { get; }

    private PtyStreamClient(Stream s, SubscribeResult result)
    {
        _s = s;
        StreamId = result.StreamId;
        BaseOffset = result.BaseOffset;
        ReplayUntilOffset = result.ReplayUntilOffset;
        TerminalModePreambleBase64 = result.TerminalModePreambleBase64;
        TerminalCheckpointBase64 = result.TerminalCheckpointBase64;
        CheckpointCols = result.CheckpointCols;
        CheckpointRows = result.CheckpointRows;
    }

    public static async Task<PtyStreamClient> SubscribeAsync(
        Func<CancellationToken, Task<Stream>> dial, string clientVersion, string channel, string nookId, ulong since, CancellationToken ct)
    {
        var s = await dial(ct);
        try
        {
            await Request(s, "cove://sys/hello",
                JsonSerializer.SerializeToElement(new HelloParams(ProtocolConstants.SemanticProtocolVersion, "gui-stream", clientVersion, channel), CoveJsonContext.Default.HelloParams), null, 1, ct);
            var sub = await Request(s, "cove://commands/nook.subscribe",
                JsonSerializer.SerializeToElement(new SubscribeParams(nookId, since), CoveJsonContext.Default.SubscribeParams), "user:gui", 2, ct);
            if (!sub.Ok || sub.Data is null) throw new InvalidOperationException($"subscribe failed: {sub.Error?.Code}");
            var r = JsonSerializer.Deserialize(sub.Data.Value, CoveJsonContext.Default.SubscribeResult)!;
            return new PtyStreamClient(s, r);
        }
        catch
        {
            await s.DisposeAsync();
            throw;
        }
    }

    private static async Task<ControlResponse> Request(
        Stream s,
        string uri,
        JsonElement paramsEl,
        string? source,
        uint seq,
        CancellationToken ct)
    {
        var id = uri.EndsWith("hello") ? "h" : "s";
        var req = new ControlRequest(id, uri, paramsEl, source);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(req, CoveJsonContext.Default.ControlRequest);
        var gate = new SemaphoreSlim(1, 1);
        await FrameIo.WriteAsync(s, gate, FrameType.Request, 0, seq, bytes, ct);
        while (true)
        {
            var f = await FrameIo.ReadAsync(s, ct);
            if (f.Type != FrameType.Response) continue;
            var resp = JsonSerializer.Deserialize(f.Payload, CoveJsonContext.Default.ControlResponse)!;
            if (resp.Id == id) return resp;
        }
    }

    public async Task PumpAsync(
        Func<StreamDataMessage, CancellationToken, Task> onData,
        Func<StreamResyncMessage, CancellationToken, Task> onResync,
        Func<StreamEndMessage, CancellationToken, Task> onEnd,
        CancellationToken ct)
    {
        while (true)
        {
            var f = await FrameIo.ReadAsync(_s, ct);
            if (f.StreamId != StreamId && f.Type is FrameType.StreamData or FrameType.Resync or FrameType.StreamEnd) continue;
            switch (f.Type)
            {
                case FrameType.StreamData:
                    await onData(StreamPayload.ReadStreamData(f.Payload), ct);
                    break;
                case FrameType.Resync:
                    await onResync(StreamPayload.ReadResync(f.Payload), ct);
                    break;
                case FrameType.StreamEnd:
                    await onEnd(StreamPayload.ReadStreamEndMessage(f.Payload), ct);
                    return;
            }
        }
    }

    public Task AckAsync(ulong ackOffset, CancellationToken ct)
    {
        var seq = Interlocked.Increment(ref _seq);
        return FrameIo.WriteAsync(_s, _writeGate, FrameType.Credit, StreamId, seq, FrameIo.U64(ackOffset), ct);
    }

    public async ValueTask DisposeAsync() => await _s.DisposeAsync();
}
