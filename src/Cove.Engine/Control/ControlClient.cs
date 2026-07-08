using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Control;

public sealed record ControlEnvelope(
    string Kind,
    int? Id,
    string? Method,
    JsonElement? Params,
    JsonElement? Result,
    JsonElement? Error,
    string? Topic,
    string? PaneId,
    long? Offset,
    int? BinaryLength);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ControlEnvelope))]
public sealed partial class ControlJsonContext : JsonSerializerContext { }

public sealed record ControlFrame(string Kind, int? Id, string? Method, JsonElement? Params, JsonElement? Result, JsonElement? Error, byte[]? Binary);

public sealed class ControlClient : IAsyncDisposable
{
    private readonly string _socketPath;
    private readonly ILogger _logger;
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private int _nextId = 1;
    private readonly Dictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly Channel<ControlFrame> _eventChannel = Channel.CreateUnbounded<ControlFrame>();
    private readonly Channel<PtyChunk> _ptyChannel = Channel.CreateUnbounded<PtyChunk>();
    private readonly CancellationTokenSource _cts = new();
    private Task? _receiveTask;

    public ControlClient(string socketPath, ILogger? logger = null)
    {
        _socketPath = socketPath;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _tcp = new TcpClient();
        await _tcp.ConnectAsync(System.Net.IPAddress.Loopback, int.Parse(_socketPath), ct).ConfigureAwait(false);
        _stream = _tcp.GetStream();
        _receiveTask = ReceiveLoopAsync();
    }

    public async Task<JsonElement> SendAsync(string method, JsonElement? parameters = null, CancellationToken ct = default)
    {
        if (_stream is null) throw new System.InvalidOperationException("not connected");

        var id = Interlocked.Increment(ref _nextId) - 1;
        var tcs = new TaskCompletionSource<JsonElement>();
        lock (_pending) _pending[id] = tcs;

        var envelope = new ControlEnvelope("req", id, method, parameters, null, null, null, null, null, null);
        var json = JsonSerializer.SerializeToUtf8Bytes(envelope, ControlJsonContext.Default.ControlEnvelope);
        var lengthPrefix = System.BitConverter.GetBytes(json.Length);
        await _stream.WriteAsync(lengthPrefix, ct).ConfigureAwait(false);
        await _stream.WriteAsync(json, ct).ConfigureAwait(false);

        using var reg = ct.Register(() => tcs.TrySetCanceled());
        return await tcs.Task.ConfigureAwait(false);
    }

    public IAsyncEnumerable<ControlFrame> SubscribeAsync(string topic, CancellationToken ct = default)
    {
        _ = SendSubscribeAsync(topic, ct);
        return _eventChannel.Reader.ReadAllAsync(ct);
    }

    private async Task SendSubscribeAsync(string topic, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse($$"""{"topic":"{{topic}}"}""");
        await SendAsync("subscribe", doc.RootElement.Clone(), ct).ConfigureAwait(false);
    }

    public IAsyncEnumerable<PtyChunk> StreamPtyAsync(string paneId, long offset, CancellationToken ct = default)
    {
        _ = SendPtyReplayAsync(paneId, offset, ct);
        return _ptyChannel.Reader.ReadAllAsync(ct);
    }

    private async Task SendPtyReplayAsync(string paneId, long offset, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse($$"""{"paneId":"{{paneId}}","offset":{{offset}}}""");
        await SendAsync("pty.replay", doc.RootElement.Clone(), ct).ConfigureAwait(false);
    }

    public async Task SendPtyInputAsync(string paneId, byte[] data, CancellationToken ct = default)
    {
        if (_stream is null) throw new System.InvalidOperationException("not connected");

        var envelope = new ControlEnvelope("pty-in", null, null, null, null, null, null, paneId, null, data.Length);
        var json = JsonSerializer.SerializeToUtf8Bytes(envelope, ControlJsonContext.Default.ControlEnvelope);
        var totalLength = json.Length + data.Length;
        var lengthPrefix = System.BitConverter.GetBytes(totalLength);

        await _stream.WriteAsync(lengthPrefix, ct).ConfigureAwait(false);
        await _stream.WriteAsync(json, ct).ConfigureAwait(false);
        await _stream.WriteAsync(data, ct).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync()
    {
        var lengthBuffer = new byte[4];
        try
        {
            while (!_cts.IsCancellationRequested && _stream is not null)
            {
                var read = await _stream.ReadAsync(lengthBuffer, _cts.Token).ConfigureAwait(false);
                if (read == 0) break;
                if (read < 4) continue;

                var totalLength = System.BitConverter.ToInt32(lengthBuffer);
                if (totalLength <= 0 || totalLength > 10 * 1024 * 1024)
                {
                    _logger.LogWarning("control: dropping frame with invalid length {len}", totalLength);
                    continue;
                }

                var buffer = new byte[totalLength];
                var totalRead = 0;
                while (totalRead < totalLength)
                {
                    var r = await _stream.ReadAsync(buffer.AsMemory(totalRead), _cts.Token).ConfigureAwait(false);
                    if (r == 0) break;
                    totalRead += r;
                }

                try
                {
                    var reader = new Utf8JsonReader(buffer.AsSpan(0, totalRead));
                    var envelope = JsonSerializer.Deserialize(ref reader, ControlJsonContext.Default.ControlEnvelope);
                    if (envelope is null)
                    {
                        _logger.LogWarning("control: received null envelope, skipping");
                        continue;
                    }

                    var jsonBytes = (int)reader.BytesConsumed;
                    var binaryData = jsonBytes < totalRead ? buffer[jsonBytes..totalRead] : null;

                    await DispatchFrameAsync(envelope, binaryData).ConfigureAwait(false);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "control: failed to parse frame envelope");
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "control: receive loop terminated");
        }
    }

    private async Task DispatchFrameAsync(ControlEnvelope envelope, byte[]? binaryData)
    {
        switch (envelope.Kind)
        {
            case "res":
                if (envelope.Id.HasValue)
                {
                    TaskCompletionSource<JsonElement>? tcs;
                    lock (_pending) _pending.Remove(envelope.Id.Value, out tcs);
                    if (tcs is not null)
                    {
                        if (envelope.Error is not null)
                            tcs.TrySetException(new ControlException(envelope.Error.Value.GetString() ?? "unknown error"));
                        else if (envelope.Result is not null)
                            tcs.TrySetResult(envelope.Result.Value.Clone());
                    }
                }
                break;

            case "evt":
                await _eventChannel.Writer.WriteAsync(new ControlFrame(
                    "evt", null, envelope.Method, envelope.Params, null, null, null)).ConfigureAwait(false);
                break;

            case "pty":
                if (binaryData is not null && envelope.PaneId is not null)
                {
                    await _ptyChannel.Writer.WriteAsync(new PtyChunk(
                        envelope.PaneId, envelope.Offset ?? 0, binaryData)).ConfigureAwait(false);
                }
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _eventChannel.Writer.TryComplete();
        _ptyChannel.Writer.TryComplete();
        _stream?.Dispose();
        _tcp?.Dispose();
        _cts.Dispose();
        if (_receiveTask is not null)
            await _receiveTask.ConfigureAwait(false);
    }
}

public sealed record PtyChunk(string PaneId, long Offset, byte[] Data);

public sealed class ControlException(string message) : Exception(message);
