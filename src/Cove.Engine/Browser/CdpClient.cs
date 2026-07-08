using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Browser;

public sealed record CdpResponse(int Id, JsonElement? Result, CdpError? Error);
public sealed record CdpError(int Code, string Message);
public sealed record CdpEvent(string Method, JsonElement Params);

public interface ICdpTransport : IAsyncDisposable
{
    Task<JsonElement> SendAsync(string method, JsonElement? parameters, CancellationToken ct = default);
    IAsyncEnumerable<CdpEvent> SubscribeAsync(string method, CancellationToken ct = default);
}

public sealed class CdpClient : ICdpTransport
{
    private readonly ClientWebSocket _ws;
    private readonly Channel<CdpEvent> _eventChannel;
    private readonly ILogger _logger;
    private int _nextId = 1;
    private readonly Dictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _receiveTask;
    private readonly Task _connectTask;

    public CdpClient(string wsUrl, ILogger? logger = null)
    {
        _ws = new ClientWebSocket();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _eventChannel = Channel.CreateUnbounded<CdpEvent>();
        _receiveTask = ReceiveLoopAsync();
        _connectTask = ConnectAsync(wsUrl);
    }

    private async Task ConnectAsync(string wsUrl)
    {
        await _ws.ConnectAsync(new Uri(wsUrl), _cts.Token).ConfigureAwait(false);
    }

    public async Task<JsonElement> SendAsync(string method, JsonElement? parameters, CancellationToken ct = default)
    {
        await _connectTask.ConfigureAwait(false);
        var id = Interlocked.Increment(ref _nextId) - 1;
        var tcs = new TaskCompletionSource<JsonElement>();
        lock (_pending) _pending[id] = tcs;
        var msg = new CdpRequest(id, method, parameters);
        var json = JsonSerializer.SerializeToUtf8Bytes(msg, CdpJsonContext.Default.CdpRequest);
        await _ws.SendAsync(json, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);

        using var reg = ct.Register(() => tcs.TrySetCanceled());
        return await tcs.Task.ConfigureAwait(false);
    }

    public async IAsyncEnumerable<CdpEvent> SubscribeAsync(string method, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var evt in _eventChannel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            if (evt.Method == method)
                yield return evt;
        }
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[65536];
        var sb = new System.Text.StringBuilder();

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                if (_ws.State != WebSocketState.Open)
                {
                    await Task.Delay(100, _cts.Token).ConfigureAwait(false);
                    continue;
                }

                var result = await _ws.ReceiveAsync(buffer, _cts.Token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                sb.Append(System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (!result.EndOfMessage)
                    continue;

                var json = sb.ToString();
                sb.Clear();

                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("id", out var idEl))
                    {
                        var id = idEl.GetInt32();
                        TaskCompletionSource<JsonElement>? tcs;
                        lock (_pending) _pending.Remove(id, out tcs);

                        if (tcs is not null)
                        {
                            if (root.TryGetProperty("error", out var errEl))
                            {
                                var code = errEl.TryGetProperty("code", out var c) ? c.GetInt32() : 0;
                                var msg = errEl.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
                                tcs.TrySetException(new CdpException(code, msg));
                            }
                            else if (root.TryGetProperty("result", out var resultEl))
                            {
                                tcs.TrySetResult(resultEl.Clone());
                            }
                        }
                    }
                    else if (root.TryGetProperty("method", out var methodEl))
                    {
                        var method = methodEl.GetString() ?? "";
                        var paramEl = root.TryGetProperty("params", out var p) ? p.Clone() : default;
                        await _eventChannel.Writer.WriteAsync(new CdpEvent(method, paramEl)).ConfigureAwait(false);
                    }
                }
                catch (JsonException ex) { _logger.LogWarning(ex, "cdp: failed to parse message"); sb.Clear(); }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogWarning(ex, "cdp: receive loop terminated"); }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _eventChannel.Writer.TryComplete();
        if (_ws.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "dispose", CancellationToken.None).ConfigureAwait(false);
        _ws.Dispose();
        _cts.Dispose();
    }
}

public sealed class CdpException(int code, string message) : Exception($"CDP error {code}: {message}")
{
    public int Code { get; } = code;
}
public sealed record CdpRequest(int Id, string Method, JsonElement? Params);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CdpRequest))]
[JsonSerializable(typeof(string))]
public sealed partial class CdpJsonContext : JsonSerializerContext { }
