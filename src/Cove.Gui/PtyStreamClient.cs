using System.Buffers.Binary;
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

    private PtyStreamClient(Stream s, ulong streamId, ulong baseOffset) { _s = s; StreamId = streamId; BaseOffset = baseOffset; }

    public static async Task<PtyStreamClient> SubscribeAsync(
        Func<CancellationToken, Task<Stream>> dial, string clientVersion, string channel, string paneId, ulong since, CancellationToken ct)
    {
        var s = await dial(ct);
        await Request(s, "cove://sys/hello",
            JsonSerializer.SerializeToElement(new HelloParams(ProtocolConstants.SemanticProtocolVersion, "gui", clientVersion, channel), CoveJsonContext.Default.HelloParams), 1, ct);
        var sub = await Request(s, "cove://commands/pane.subscribe",
            JsonSerializer.SerializeToElement(new SubscribeParams(paneId, since), CoveJsonContext.Default.SubscribeParams), 2, ct);
        if (!sub.Ok || sub.Data is null) throw new InvalidOperationException($"subscribe failed: {sub.Error?.Code}");
        var r = JsonSerializer.Deserialize(sub.Data.Value, CoveJsonContext.Default.SubscribeResult)!;
        return new PtyStreamClient(s, r.StreamId, r.BaseOffset);
    }

    private static async Task<ControlResponse> Request(Stream s, string uri, JsonElement paramsEl, uint seq, CancellationToken ct)
    {
        var id = uri.EndsWith("hello") ? "h" : "s";
        var req = new ControlRequest(id, uri, paramsEl, "user:gui");
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
        Func<ulong, ReadOnlyMemory<byte>, CancellationToken, Task> onData,
        Func<ulong, CancellationToken, Task> onResync,
        Func<ulong, int, CancellationToken, Task> onEnd,
        CancellationToken ct)
    {
        while (true)
        {
            var f = await FrameIo.ReadAsync(_s, ct);
            if (f.StreamId != StreamId && f.Type is FrameType.StreamData or FrameType.Resync or FrameType.StreamEnd) continue;
            switch (f.Type)
            {
                case FrameType.StreamData:
                    var offset = BinaryPrimitives.ReadUInt64LittleEndian(f.Payload);
                    await onData(offset, f.Payload.AsMemory(8), ct);
                    break;
                case FrameType.Resync:
                    await onResync(BinaryPrimitives.ReadUInt64LittleEndian(f.Payload), ct);
                    break;
                case FrameType.StreamEnd:
                    var final = BinaryPrimitives.ReadUInt64LittleEndian(f.Payload);
                    var code = BinaryPrimitives.ReadInt32LittleEndian(f.Payload.AsSpan(8));
                    await onEnd(final, code, ct);
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
