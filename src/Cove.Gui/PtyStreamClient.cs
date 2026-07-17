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
        await Request(s, "cove://sys/hello",
            JsonSerializer.SerializeToElement(new HelloParams(ProtocolConstants.SemanticProtocolVersion, "gui", clientVersion, channel), CoveJsonContext.Default.HelloParams), 1, ct);
        var sub = await Request(s, "cove://commands/nook.subscribe",
            JsonSerializer.SerializeToElement(new SubscribeParams(nookId, since), CoveJsonContext.Default.SubscribeParams), 2, ct);
        if (!sub.Ok || sub.Data is null) throw new InvalidOperationException($"subscribe failed: {sub.Error?.Code}");
        var r = JsonSerializer.Deserialize(sub.Data.Value, CoveJsonContext.Default.SubscribeResult)!;
        return new PtyStreamClient(s, r);
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
        Func<ulong, string, string, int, int, CancellationToken, Task> onResync,
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
                    if (f.Payload.Length < 8)
                        throw new InvalidDataException($"StreamData payload is too short: expected at least 8 bytes, received {f.Payload.Length}");
                    var offset = BinaryPrimitives.ReadUInt64LittleEndian(f.Payload);
                    await onData(offset, f.Payload.AsMemory(8), ct);
                    break;
                case FrameType.Resync:
                    if (f.Payload.Length < 8)
                        throw new InvalidDataException($"Resync payload is too short: expected at least 8 bytes, received {f.Payload.Length}");
                    ulong newBase = BinaryPrimitives.ReadUInt64LittleEndian(f.Payload);
                    if (f.Payload.Length < 20)
                    {
                        await onResync(newBase, f.Payload.Length > 8 ? Convert.ToBase64String(f.Payload.AsSpan(8)) : "", "", 0, 0, ct);
                        break;
                    }
                    int cols = BinaryPrimitives.ReadInt32LittleEndian(f.Payload.AsSpan(8));
                    int rows = BinaryPrimitives.ReadInt32LittleEndian(f.Payload.AsSpan(12));
                    int modeLength = BinaryPrimitives.ReadInt32LittleEndian(f.Payload.AsSpan(16));
                    if (modeLength < 0 || modeLength > f.Payload.Length - 20)
                        throw new InvalidDataException("invalid resync mode length");
                    string modes = modeLength == 0 ? "" : Convert.ToBase64String(f.Payload.AsSpan(20, modeLength));
                    string checkpoint = modeLength == f.Payload.Length - 20 ? "" : Convert.ToBase64String(f.Payload.AsSpan(20 + modeLength));
                    await onResync(newBase, modes, checkpoint, cols, rows, ct);
                    break;
                case FrameType.StreamEnd:
                    if (f.Payload.Length < 12)
                        throw new InvalidDataException($"StreamEnd payload is too short: expected at least 12 bytes, received {f.Payload.Length}");
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
