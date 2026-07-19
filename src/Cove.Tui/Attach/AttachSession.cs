using System.Text.Json;
using Cove.Protocol;

namespace Cove.Tui.Attach;

public sealed class AttachSession
{
    private readonly FrameConnection _conn;
    private readonly string _nookId;
    private readonly string _clientKind;
    private readonly string _clientVersion;
    private readonly string _channel;
    private readonly string _source;
    private ulong _streamId;
    private ulong _ackedOffset;
    private uint _seq;
    private int _requestId;
    private bool _helloCompleted;

    public ulong StreamId => _streamId;
    public ulong AckedOffset => _ackedOffset;

    public AttachSession(
        FrameConnection conn,
        string nookId,
        string clientKind,
        string clientVersion,
        string channel,
        string source)
    {
        _conn = conn;
        _nookId = nookId;
        _clientKind = clientKind;
        _clientVersion = clientVersion;
        _channel = channel;
        _source = source;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        if (_helloCompleted)
            return;
        var helloEl = JsonSerializer.SerializeToElement(
            new HelloParams(ProtocolConstants.SemanticProtocolVersion, _clientKind, _clientVersion, _channel),
            CoveJsonContext.Default.HelloParams);
        await SendRequestAsync("h", "cove://sys/hello", helloEl, null, ct).ConfigureAwait(false);
        var response = await ReadResponseAsync("h", ct).ConfigureAwait(false);
        if (!response.Ok)
            throw new InvalidOperationException($"hello failed: {response.Error?.Code}");
        _helloCompleted = true;
    }

    public async Task<SubscribeResult> SubscribeAsync(CancellationToken ct)
    {
        await ConnectAsync(ct).ConfigureAwait(false);
        var subEl = JsonSerializer.SerializeToElement(new SubscribeParams(_nookId, 0), CoveJsonContext.Default.SubscribeParams);
        await SendRequestAsync("s", "cove://commands/nook.subscribe", subEl, _source, ct).ConfigureAwait(false);
        var subResp = await ReadResponseAsync("s", ct).ConfigureAwait(false);
        if (!subResp.Ok || subResp.Data is null)
            throw new InvalidOperationException($"subscribe failed: {subResp.Error?.Code}");
        var result = JsonSerializer.Deserialize(subResp.Data.Value, CoveJsonContext.Default.SubscribeResult)
            ?? throw new InvalidOperationException("subscribe returned null result");
        _streamId = result.StreamId;
        _ackedOffset = result.BaseOffset;
        return result;
    }

    public async Task<ControlResponse> RequestAsync(
        string uri,
        JsonElement? parameters,
        CancellationToken ct)
    {
        await ConnectAsync(ct).ConfigureAwait(false);
        var id = "r" + Interlocked.Increment(ref _requestId);
        await SendRequestAsync(id, uri, parameters, _source, ct).ConfigureAwait(false);
        return await ReadResponseAsync(id, ct).ConfigureAwait(false);
    }

    public async Task PumpAsync(
        Func<StreamDataMessage, CancellationToken, Task> onData,
        Func<StreamResyncMessage, CancellationToken, Task> onResync,
        Func<StreamEndMessage, CancellationToken, Task> onEnd,
        CancellationToken ct)
    {
        while (true)
        {
            var f = await _conn.ReadFrameAsync(ct).ConfigureAwait(false);
            if (f is null) return;
            if (AttachFrameDecode.IsStreamFrame(f.Value.Header.Type) && !AttachFrameDecode.BelongsToStream(f.Value.Header.StreamId, _streamId, f.Value.Header.Type)) continue;
            switch (f.Value.Header.Type)
            {
                case FrameType.StreamData:
                    var data = StreamPayload.ReadStreamData(f.Value.Payload);
                    await onData(data, ct).ConfigureAwait(false);
                    _ackedOffset = AttachFrameDecode.NextAckOffset(_ackedOffset, data.Offset, data.Data.Length);
                    await AckAsync(_ackedOffset, ct).ConfigureAwait(false);
                    break;
                case FrameType.Resync:
                    var resync = StreamPayload.ReadResync(f.Value.Payload);
                    _ackedOffset = resync.BaseOffset;
                    await onResync(resync, ct).ConfigureAwait(false);
                    break;
                case FrameType.StreamEnd:
                    await onEnd(StreamPayload.ReadStreamEndMessage(f.Value.Payload), ct).ConfigureAwait(false);
                    return;
            }
        }
    }

    public async Task SendInputAsync(byte[] data, CancellationToken ct)
    {
        if (data.Length == 0) return;
        var writeEl = JsonSerializer.SerializeToElement(new NookWriteParams(_nookId, toBase64(data)), CoveJsonContext.Default.NookWriteParams);
        await SendRequestAsync("w" + (++_seq), "cove://commands/nook.write", writeEl, _source, ct).ConfigureAwait(false);
    }

    public async Task AckAsync(ulong ackOffset, CancellationToken ct)
    {
        var payload = AttachFrameDecode.EncodeCredit(ackOffset);
        await _conn.WriteFrameAsync(FrameType.Credit, _streamId, payload, ct).ConfigureAwait(false);
    }

    private async Task SendRequestAsync(
        string id,
        string uri,
        JsonElement? parameters,
        string? source,
        CancellationToken ct)
    {
        var req = new ControlRequest(id, uri, parameters, source);
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
