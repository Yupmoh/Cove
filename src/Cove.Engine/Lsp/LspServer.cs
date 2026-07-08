using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Lsp;

public sealed record LspRequest(int Id, string Method, JsonElement? Params);
public sealed record LspResponse(int Id, JsonElement? Result, JsonElement? Error);
public sealed record LspNotification(string Method, JsonElement? Params);
public sealed record LspServerConfig(string Command, string[] Args, string[] Languages);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LspRequest))]
[JsonSerializable(typeof(LspResponse))]
[JsonSerializable(typeof(LspNotification))]
public sealed partial class LspJsonContext : JsonSerializerContext { }

public sealed class LspServer : IAsyncDisposable
{
    private readonly LspServerConfig _config;
    private readonly ILogger _logger;
    private Process? _process;
    private System.Threading.Channels.Channel<LspNotification> _notifications;
    private readonly Dictionary<int, TaskCompletionSource<JsonElement?>> _pending = new();
    private int _nextId = 1;
    private Task? _readTask;
    private readonly System.Threading.CancellationTokenSource _cts = new();

    public bool IsRunning { get { try { return _process is not null && !_process.HasExited; } catch { return false; } } }

    public LspServer(LspServerConfig config, ILogger logger)
    {
        _config = config;
        _logger = logger;
        _notifications = System.Threading.Channels.Channel.CreateUnbounded<LspNotification>();
    }

    public async Task<bool> StartAsync(CancellationToken ct = default)
    {
        if (_process is not null && !_process.HasExited) return true;

        var psi = new ProcessStartInfo(_config.Command)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in _config.Args)
            psi.ArgumentList.Add(arg);

        try
        {
            _process = new Process { StartInfo = psi };
            _process.Start();
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "lsp: failed to start {cmd}", _config.Command);
            return false;
        }

        _readTask = ReadLoopAsync();
        return await InitializeAsync(ct);
    }

    private async Task<bool> InitializeAsync(CancellationToken ct)
    {
        var initParams = $$"""{"processId":{{Environment.ProcessId}},"rootUri":"","capabilities":{} }""";
        using var doc = System.Text.Json.JsonDocument.Parse(initParams);
        var result = await SendRequestAsync("initialize", doc.RootElement.Clone(), ct);
        if (result is null)
        {
            _logger.LogWarning("lsp: initialize failed for {cmd}", _config.Command);
            return false;
        }
        using var initDoc = System.Text.Json.JsonDocument.Parse("{}");
        await SendNotificationAsync("initialized", initDoc.RootElement.Clone(), ct);
        return true;
    }

    public async Task<JsonElement?> SendRequestAsync(string method, JsonElement? parameters, CancellationToken ct = default)
    {
        if (_process is null || _process.HasExited)
        {
            _logger.LogWarning("lsp: server not running, cannot send {method}", method);
            return null;
        }

        var id = System.Threading.Interlocked.Increment(ref _nextId) - 1;
        var tcs = new TaskCompletionSource<JsonElement?>();
        lock (_pending) _pending[id] = tcs;

        var request = new LspRequest(id, method, parameters);
        await WriteMessageAsync(request, ct);

        using var reg = ct.Register(() => tcs.TrySetCanceled());
        return await tcs.Task;
    }

    public async Task SendNotificationAsync(string method, JsonElement? parameters, CancellationToken ct = default)
    {
        if (_process is null || _process.HasExited)
        {
            _logger.LogWarning("lsp: server not running, notification {method} dropped", method);
            return;
        }
        var notif = new LspNotification(method, parameters);
        await WriteNotificationAsync(notif, ct);
    }

    private async Task WriteNotificationAsync(LspNotification notif, CancellationToken ct)
    {
        if (_process is null) return;
        var json = JsonSerializer.Serialize(notif, LspJsonContext.Default.LspNotification);
        var content = System.Text.Encoding.UTF8.GetBytes(json);
        var header = System.Text.Encoding.ASCII.GetBytes($"Content-Length: {content.Length}\r\n\r\n");
        await _process.StandardInput.BaseStream.WriteAsync(header, ct);
        await _process.StandardInput.BaseStream.WriteAsync(content, ct);
        await _process.StandardInput.BaseStream.FlushAsync(ct);
    }


    public System.Collections.Generic.IAsyncEnumerable<LspNotification> Notifications => _notifications.Reader.ReadAllAsync();

    private async Task WriteMessageAsync(LspRequest request, CancellationToken ct)
    {
        if (_process is null) return;
        var json = JsonSerializer.Serialize(request, LspJsonContext.Default.LspRequest);
        var content = System.Text.Encoding.UTF8.GetBytes(json);
        var header = System.Text.Encoding.ASCII.GetBytes($"Content-Length: {content.Length}\r\n\r\n");
        await _process.StandardInput.BaseStream.WriteAsync(header, ct);
        await _process.StandardInput.BaseStream.WriteAsync(content, ct);
        await _process.StandardInput.BaseStream.FlushAsync(ct);
    }

    private async Task ReadLoopAsync()
    {
        var stream = _process?.StandardOutput.BaseStream;
        if (stream is null) return;
        var headerBuffer = new byte[64];

        try
        {
            while (!_cts.IsCancellationRequested && _process is not null && !_process.HasExited)
            {
                var contentLength = await ReadContentLengthAsync(stream, _cts.Token);
                if (contentLength <= 0) break;

                var content = new byte[contentLength];
                var totalRead = 0;
                while (totalRead < contentLength)
                {
                    var read = await stream.ReadAsync(content.AsMemory(totalRead), _cts.Token);
                    if (read == 0) break;
                    totalRead += read;
                }

                try
                {
                    using var doc = JsonDocument.Parse(content.AsMemory(0, totalRead));
                    var root = doc.RootElement;
                    if (root.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var id))
                    {
                        TaskCompletionSource<JsonElement?>? tcs;
                        lock (_pending) _pending.Remove(id, out tcs);
                        if (tcs is not null)
                        {
                            if (root.TryGetProperty("error", out var err))
                                tcs.TrySetException(new LspException(err.GetRawText()));
                            else
                                tcs.TrySetResult(root.TryGetProperty("result", out var result) ? result.Clone() : null);
                        }
                    }
                    else if (root.TryGetProperty("method", out var methodEl))
                    {
                        var method = methodEl.GetString() ?? "";
                        var paramEl = root.TryGetProperty("params", out var p) ? p.Clone() : (JsonElement?)null;
                        await _notifications.Writer.WriteAsync(new LspNotification(method, paramEl), _cts.Token);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "lsp: failed to parse message");
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "lsp: read loop terminated for {cmd}", _config.Command);
        }
    }

    private static async Task<int> ReadContentLengthAsync(System.IO.Stream stream, CancellationToken ct)
    {
        var lineBuilder = new System.Text.StringBuilder();
        var byteBuf = new byte[1];
        int contentLength = 0;

        while (true)
        {
            var read = await stream.ReadAsync(byteBuf, ct);
            if (read == 0) return 0;
            var c = (char)byteBuf[0];
            lineBuilder.Append(c);
            if (lineBuilder.ToString().EndsWith("\r\n\r\n"))
            {
                var header = lineBuilder.ToString();
                var match = System.Text.RegularExpressions.Regex.Match(header, @"Content-Length:\s*(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out contentLength))
                    return contentLength;
                return 0;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _notifications.Writer.TryComplete();
        if (_process is not null)
        {
            try { if (!_process.HasExited) _process.Kill(); } catch { }
        }
        _process?.Dispose();
        _cts.Dispose();
        if (_readTask is not null)
            await _readTask.ConfigureAwait(false);
    }
}

public sealed class LspException(string message) : Exception(message);
