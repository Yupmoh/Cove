using System.Buffers.Binary;
using System.Text.Json;
using Cove.Protocol;

namespace Cove.Tui.Attach;

public sealed class AttachSession
{
    private readonly FrameConnection _conn;
    private readonly string _paneId;
    private ulong _streamId;
    private ulong _ackedOffset;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private uint _seq;

    public ulong StreamId => _streamId;
    public ulong AckedOffset => _ackedOffset;

    public AttachSession(FrameConnection conn, string paneId)
    {
        _conn = conn;
        _paneId = paneId;
    }

    public async Task<SubscribeResult> SubscribeAsync(string clientKind, CancellationToken ct)
    {
        var helloEl = JsonSerializer.SerializeToElement(
            new HelloParams(ProtocolConstants.SemanticProtocolVersion, clientKind, "0.1.0", "stable"),
            CoveJsonContext.Default.HelloParams);
        await SendRequestAsync("h", "cove://sys/hello", helloEl, ct).ConfigureAwait(false);
        await ReadResponseAsync("h", ct).ConfigureAwait(false);

        var subEl = JsonSerializer.SerializeToElement(new SubscribeParams(_paneId, 0), CoveJsonContext.Default.SubscribeParams);
        var subResp = await ReadResponseAsync("s", ct).ConfigureAwait(false);
        await SendRequestAsync("s", "cove://commands/pane.subscribe", subEl, ct).ConfigureAwait(false);
        if (!subResp.Ok || subResp.Data is null)
            throw new InvalidOperationException($"subscribe failed: {subResp.Error?.Code}");
        var result = JsonSerializer.Deserialize(subResp.Data.Value, CoveJsonContext.Default.SubscribeResult)
            ?? throw new InvalidOperationException("subscribe returned null result");
        _streamId = result.StreamId;
        _ackedOffset = result.BaseOffset;
        return result;
    }

    public async Task PumpAsync(Func<ReadOnlyMemory<byte>, CancellationToken, Task> onData, Func<ulong, int, CancellationToken, Task> onEnd, CancellationToken ct)
    {
        while (true)
        {
            var f = await _conn.ReadFrameAsync(ct).ConfigureAwait(false);
            if (f is null) return;
            if (AttachFrameDecode.IsStreamFrame(f.Value.Header.Type) && !AttachFrameDecode.BelongsToStream(f.Value.Header.StreamId, _streamId, f.Value.Header.Type)) continue;
            switch (f.Value.Header.Type)
            {
                case FrameType.StreamData:
                    var offset = BinaryPrimitives.ReadUInt64LittleEndian(f.Value.Payload);
                    var raw = f.Value.Payload.AsMemory(8);
                    await onData(raw, ct).ConfigureAwait(false);
                    _ackedOffset = offset + (ulong)raw.Length;
                    await AckAsync(_ackedOffset, ct).ConfigureAwait(false);
                    break;
                case FrameType.Resync:
                    _ackedOffset = BinaryPrimitives.ReadUInt64LittleEndian(f.Value.Payload);
                    break;
                case FrameType.StreamEnd:
                    var finalOffset = BinaryPrimitives.ReadUInt64LittleEndian(f.Value.Payload);
                    var exitCode = BinaryPrimitives.ReadInt32LittleEndian(f.Value.Payload.AsSpan(8));
                    await onEnd(finalOffset, exitCode, ct).ConfigureAwait(false);
                    return;
            }
        }
    }

    public async Task SendInputAsync(byte[] data, CancellationToken ct)
    {
        if (data.Length == 0) return;
        var writeEl = JsonSerializer.SerializeToElement(new PaneWriteParams(_paneId, toBase64(data)), CoveJsonContext.Default.PaneWriteParams);
        await SendRequestAsync("w" + (++_seq), "cove://commands/pane.write", writeEl, ct).ConfigureAwait(false);
    }

    public async Task AckAsync(ulong ackOffset, CancellationToken ct)
    {
        var seq = Interlocked.Increment(ref _seq);
        var payload = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(payload, ackOffset);
        await _conn.WriteFrameAsync(FrameType.Credit, _streamId, payload, ct).ConfigureAwait(false);
    }

    private async Task SendRequestAsync(string id, string uri, JsonElement? parameters, CancellationToken ct)
    {
        var req = new ControlRequest(id, uri, parameters, "user:tui");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(req, CoveJsonContext.Default.ControlRequest);
        await _conn.WriteFrameAsync(FrameType.Request, 0, bytes, ct).ConfigureAwait(false);
    }

    private async Task<ControlResponse> ReadResponseAsync(string expectedId, CancellationToken ct)
    {
        while (true)
        {
            var f = await _conn.ReadFrameAsync(ct).ConfigureAwait(false);
            if (f is null) throw new IOException("connection closed");
            if (f.Value.Header.Type != FrameType.Response) continue;
            var resp = JsonSerializer.Deserialize(f.Value.Payload, CoveJsonContext.Default.ControlResponse)!;
            if (resp.Id == expectedId) return resp;
        }
    }

    private static string toBase64(byte[] data) => System.Convert.ToBase64String(data);
}
