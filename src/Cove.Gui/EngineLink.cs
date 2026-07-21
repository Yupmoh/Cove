using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Cove.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cove.Gui;

public sealed class EngineLink : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<Stream>> _dial;
    private readonly string _clientVersion;
    private readonly string _channel;
    private readonly string _endpoint;
    private readonly string? _providedControlToken;
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ControlResponse>> _pending = new();
    private ILogger _log = NullLogger.Instance;
    private System.Action<string, JsonElement>? _onEngineEvent;
    private Stream? _stream;
    private CancellationTokenSource? _readPumpCts;
    private Task? _readPump;
    private bool _everConnected;
    private uint _seq;
    private int _idCounter;

    public EngineLink(
        Func<CancellationToken, Task<Stream>> dial,
        string clientVersion,
        string channel,
        string? controlToken = null)
    {
        _dial = dial;
        _clientVersion = clientVersion;
        _channel = channel;
        _endpoint = GuiLogging.EndpointFor(channel);
        _providedControlToken = controlToken;
    }

    public void SetLogger(ILogger logger) => _log = logger;

    public string Channel => _channel;

    public void SetEngineEventHandler(System.Action<string, JsonElement> handler) { _onEngineEvent = handler; }

    public EngineLink CreateBackgroundLink()
        => new(_dial, _clientVersion, _channel, _providedControlToken);

    public async Task<ControlResponse> RequestAsync(string uri, JsonElement? paramsEl, CancellationToken ct)
    {
        var s = await EnsureConnectedAsync(ct);
        return await SendRequestAsync(s, uri, paramsEl, "user:gui", ct);
    }

    private string? ReadControlToken()
    {
        if (!string.IsNullOrEmpty(_providedControlToken))
            return _providedControlToken;
        var dd = Cove.Platform.CoveDataDir.Resolve(GuiLogging.ParseChannel(_channel));
        try
        {
            return Cove.Platform.ControlCredential.Read(dd);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            _log.ControlTokenMissing(dd.ControlTokenPath);
            return null;
        }
        catch (Exception ex)
        {
            _log.ControlTokenReadFailed(ex.Message);
            return null;
        }
    }

    private async Task<Stream> EnsureConnectedAsync(CancellationToken ct)
    {
        if (_stream is not null) return _stream;
        await _connectGate.WaitAsync(ct);
        try
        {
            if (_stream is not null) return _stream;
            var previousPump = _readPump;
            var previousPumpCts = _readPumpCts;
            if (previousPump is not null)
                await previousPump;
            previousPumpCts?.Dispose();
            _readPumpCts = null;
            _readPump = null;
            if (_everConnected) _log.EngineReconnecting(_channel, _endpoint);
            else _log.EngineConnecting(_channel, _endpoint);
            var s = await _dial(ct);
            var pumpCts = new CancellationTokenSource();
            var readPump = Task.Run(() => ReadPumpAsync(s, pumpCts.Token));
            try
            {
                var helloEl = JsonSerializer.SerializeToElement(
                    new HelloParams(ProtocolConstants.SemanticProtocolVersion, "gui", _clientVersion, _channel, ControlToken: ReadControlToken()),
                    CoveJsonContext.Default.HelloParams);
                var hello = await SendRequestAsync(s, "cove://sys/hello", helloEl, null, ct);
                if (!hello.Ok)
                {
                    _log.EngineHelloRejected(_channel, _endpoint, hello.Error?.Code ?? "unknown");
                    throw new InvalidOperationException($"hello failed: {hello.Error?.Code}");
                }
                var helloResult = hello.Data?.Deserialize(
                    CoveJsonContext.Default.HelloResult);
                var engineVersion = helloResult?.EngineVersion ?? "";
                if (!string.Equals(
                        engineVersion,
                        _clientVersion,
                        StringComparison.Ordinal))
                {
                    _log.EngineVersionMismatch(
                        _channel,
                        _endpoint,
                        _clientVersion,
                        engineVersion);
                    throw new InvalidOperationException(
                        $"engine version mismatch: expected {_clientVersion}, actual {engineVersion}");
                }
                if (readPump.IsCompleted)
                    throw new IOException("control connection closed during hello");
                _readPumpCts = pumpCts;
                _readPump = readPump;
                Volatile.Write(ref _stream, s);
                if (readPump.IsCompleted)
                {
                    Interlocked.CompareExchange(ref _stream, null, s);
                    throw new IOException("control connection closed during hello");
                }
                var wasConnected = _everConnected;
                _everConnected = true;
                _log.EngineConnected(_channel, _endpoint, engineVersion);
                if (wasConnected)
                {
                    using var reconnectPayload = JsonDocument.Parse("{}");
                    _onEngineEvent?.Invoke("engine.reconnected", reconnectPayload.RootElement.Clone());
                }
                return s;
            }
            catch
            {
                await pumpCts.CancelAsync();
                await s.DisposeAsync();
                await readPump;
                if (ReferenceEquals(_readPump, readPump))
                {
                    _readPump = null;
                    _readPumpCts = null;
                }
                pumpCts.Dispose();
                throw;
            }
        }
        finally { _connectGate.Release(); }
    }

    private async Task<ControlResponse> SendRequestAsync(
        Stream s,
        string uri,
        JsonElement? paramsEl,
        string? source,
        CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _idCounter).ToString();
        var tcs = new TaskCompletionSource<ControlResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        var req = new ControlRequest(id, uri, paramsEl, source);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(req, CoveJsonContext.Default.ControlRequest);
        var seq = Interlocked.Increment(ref _seq);
        var sw = Stopwatch.StartNew();
        try
        {
            await FrameIo.WriteAsync(s, _writeGate, FrameType.Request, 0, seq, bytes, ct);
            using var timeout = new CancellationTokenSource(ProtocolConstants.ControlRequestTimeoutMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            await using (linked.Token.Register(() => tcs.TrySetCanceled()))
            {
                var response = await tcs.Task;
                _log.EngineRequest(uri, sw.ElapsedMilliseconds);
                return response;
            }
        }
        catch (Exception ex)
        {
            _pending.TryRemove(id, out _);
            _log.EngineRequestFailed(uri, ex.Message);
            throw;
        }
    }

    private async Task ReadPumpAsync(Stream s, CancellationToken ct)
    {
        try
        {
            while (true)
            {
                var f = await FrameIo.ReadAsync(s, ct);
                if (f.Type == FrameType.Response)
                {
                    var r = JsonSerializer.Deserialize(f.Payload, CoveJsonContext.Default.ControlResponse)!;
                    if (_pending.TryRemove(r.Id, out var tcs)) tcs.TrySetResult(r);
                }
                else if (f.Type == FrameType.Error)
                {
                    var e = JsonSerializer.Deserialize(f.Payload, CoveJsonContext.Default.ControlErrorFrame)!;
                    _log.EngineControlError(e.Code, e.Message, e.StreamId?.ToString() ?? "");
                    if (e.StreamId is null) break;
                }
                else if (f.Type == FrameType.Event)
                {
                    try
                    {
                        var evt = JsonSerializer.Deserialize(f.Payload, CoveJsonContext.Default.ControlEvent);
                        if (evt is not null) _onEngineEvent?.Invoke(evt.Channel, evt.Payload);
                    }
                    catch (Exception ex) { _log.EngineEventDeserializeFailed(ex.Message); }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { _log.EngineReadPumpEnded(ex.Message); }
        foreach (var kv in _pending) kv.Value.TrySetException(new IOException("control connection closed"));
        _pending.Clear();
        Interlocked.CompareExchange(ref _stream, null, s);
        await s.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _connectGate.WaitAsync();
        try
        {
            var pumpCts = _readPumpCts;
            var pump = _readPump;
            var stream = Interlocked.Exchange(ref _stream, null);
            if (pumpCts is not null)
                await pumpCts.CancelAsync();
            if (stream is not null)
                await stream.DisposeAsync();
            if (pump is not null)
                await pump;
            if (ReferenceEquals(_readPump, pump))
            {
                _readPump = null;
                _readPumpCts = null;
            }
            pumpCts?.Dispose();
        }
        finally
        {
            _connectGate.Release();
        }
    }
}
