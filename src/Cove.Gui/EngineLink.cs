using System.Collections.Concurrent;
using System.Text.Json;
using Cove.Protocol;

namespace Cove.Gui;

public sealed class EngineLink : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<Stream>> _dial;
    private readonly string _clientVersion;
    private readonly string _channel;
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ControlResponse>> _pending = new();
    private System.Action<string, JsonElement>? _onEngineEvent;
    private Stream? _stream;
    private uint _seq;
    private int _idCounter;

    public EngineLink(Func<CancellationToken, Task<Stream>> dial, string clientVersion, string channel)
    { _dial = dial; _clientVersion = clientVersion; _channel = channel; }

    public string Channel => _channel;

    public void SetEngineEventHandler(System.Action<string, JsonElement> handler) { _onEngineEvent = handler; }

    public async Task<ControlResponse> RequestAsync(string uri, JsonElement? paramsEl, CancellationToken ct)
    {
        var s = await EnsureConnectedAsync(ct);
        return await SendRequestAsync(s, uri, paramsEl, ct);
    }

    private async Task<Stream> EnsureConnectedAsync(CancellationToken ct)
    {
        if (_stream is not null) return _stream;
        await _connectGate.WaitAsync(ct);
        try
        {
            if (_stream is not null) return _stream;
            var s = await _dial(ct);
            _ = Task.Run(() => ReadPumpAsync(s));
            var helloEl = JsonSerializer.SerializeToElement(
                new HelloParams(ProtocolConstants.SemanticProtocolVersion, "gui", _clientVersion, _channel),
                CoveJsonContext.Default.HelloParams);
            var hello = await SendRequestAsync(s, "cove://sys/hello", helloEl, ct);
            if (!hello.Ok) throw new InvalidOperationException($"hello failed: {hello.Error?.Code}");
            _stream = s;
            return s;
        }
        finally { _connectGate.Release(); }
    }

    private async Task<ControlResponse> SendRequestAsync(Stream s, string uri, JsonElement? paramsEl, CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _idCounter).ToString();
        var tcs = new TaskCompletionSource<ControlResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        var req = new ControlRequest(id, uri, paramsEl, "user:gui");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(req, CoveJsonContext.Default.ControlRequest);
        var seq = Interlocked.Increment(ref _seq);
        await FrameIo.WriteAsync(s, _writeGate, FrameType.Request, 0, seq, bytes, ct);
        using var timeout = new CancellationTokenSource(ProtocolConstants.ControlRequestTimeoutMs);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        await using (linked.Token.Register(() => tcs.TrySetCanceled()))
            return await tcs.Task;
    }

    private async Task ReadPumpAsync(Stream s)
    {
        try
        {
            while (true)
            {
                var f = await FrameIo.ReadAsync(s, CancellationToken.None);
                if (f.Type == FrameType.Response)
                {
                    var r = JsonSerializer.Deserialize(f.Payload, CoveJsonContext.Default.ControlResponse)!;
                    if (_pending.TryRemove(r.Id, out var tcs)) tcs.TrySetResult(r);
                }
                else if (f.Type == FrameType.Error)
                {
                    var e = JsonSerializer.Deserialize(f.Payload, CoveJsonContext.Default.ControlErrorFrame)!;
                    Console.Error.WriteLine($"control error frame: {e.Code} {e.Message}");
                    if (e.StreamId is null) break;
                }
                else if (f.Type == FrameType.Event)
                {
                    try
                    {
                        var evt = JsonSerializer.Deserialize(f.Payload, CoveJsonContext.Default.ControlEvent);
                        if (evt is not null) _onEngineEvent?.Invoke(evt.Channel, evt.Payload);
                    }
                    catch (Exception ex) { Console.Error.WriteLine($"event forward failed: {ex.Message}"); }
                }
            }
        }
        catch (Exception ex) { Console.Error.WriteLine($"control read pump ended: {ex.Message}"); }
        foreach (var kv in _pending) kv.Value.TrySetException(new IOException("control connection closed"));
        _pending.Clear();
        _stream = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream is not null) { await _stream.DisposeAsync(); _stream = null; }
    }
}
