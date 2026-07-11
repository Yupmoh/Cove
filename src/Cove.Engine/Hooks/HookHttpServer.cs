using System.Net;
using System.Text.Json;
using Cove.Adapters;
using Microsoft.Extensions.Logging;


namespace Cove.Engine.Hooks;

public sealed class HookHttpServer : IDisposable
{
    private readonly string _dataDir;
    public string DataDir => _dataDir;
    private readonly ILogger? _logger;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private JsonElement? _context;
    private bool _disposed;

    public event Action<HookEvent>? OnEvent;
    public int Port { get; private set; }
    public bool IsRunning => _listener is not null;

    public HookHttpServer(string dataDir, ILogger? logger = null)
    {
        _dataDir = dataDir;
        _logger = logger;
    }

    public void SetContext(JsonElement context) => _context = context;
    public ContextInjector? Injector { get; set; }
    public AmbientContextAggregator? Aggregator { get; set; }

    public async Task StartAsync()
    {
        Directory.CreateDirectory(_dataDir);
        Port = PickFreePort();

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
                _listener.Start();
                break;
            }
            catch (HttpListenerException) when (attempt < 2)
            {
                Port = PickFreePort();
                _listener = null;
            }
        }

        if (_listener is null)
            throw new InvalidOperationException("failed to start hook HTTP server after 3 attempts");

        var portFile = Path.Combine(_dataDir, "hook-port");
        await File.WriteAllTextAsync(portFile, Port.ToString());

        _cts = new CancellationTokenSource();
        _listenTask = ListenLoop(_cts.Token);
    }

    private static int PickFreePort()
    {
        using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is not null)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().WaitAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException ex) { _logger?.HookListenError(ex.Message); break; }

            _ = Task.Run(() => HandleRequest(ctx));
        }
    }

    private void HandleRequest(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url?.AbsolutePath ?? "";
        var method = ctx.Request.HttpMethod;

        try
        {
            if (method == "POST" && path.StartsWith("/api/adapter/", StringComparison.Ordinal))
            {
                RespondEmit(ctx);
                return;
            }

            if (method == "POST" && path == "/resolve")
            {
                RespondResolve(ctx);
                return;
            }

            ctx.Response.StatusCode = 404;
            ctx.Response.Close();
        }
        catch (Exception ex)
        {
            _logger?.HookServerError(ex.Message);
            try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch (Exception closeEx) { _logger?.HookRequestError(closeEx.Message); }
        }
    }

    private void RespondEmit(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url!.AbsolutePath;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 4 || segments[0] != "api" || segments[1] != "adapter")
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.Close();
            return;
        }

        var adapter = segments[2];
        var eventName = segments[3];
        var nookId = ctx.Request.Headers["X-Cove-Nook-Id"];

        JsonElement? payload = null;
        if (ctx.Request.HasEntityBody && ctx.Request.ContentLength64 > 0)
        {
            using var reader = new StreamReader(ctx.Request.InputStream);
            var body = reader.ReadToEnd();
            if (!string.IsNullOrEmpty(body))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    payload = doc.RootElement.Clone();
                }
                catch (JsonException ex)
                {
                    _logger?.HookPayloadInvalid(adapter, eventName, ex.Message);
                }
            }
        }

        var hookEvent = new HookEvent
        {
            Adapter = adapter,
            Event = eventName,
            NookId = nookId,
            Payload = payload,
        };

        try { OnEvent?.Invoke(hookEvent); }
        catch (Exception ex) { _logger?.HookHandlerFailed(adapter, eventName, ex.Message); }

        if (nookId is not null)
            ctx.Response.Headers["X-Cove-Nook-Id"] = nookId;

        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "application/json";
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            if (_context is { } contextEl)
            {
                writer.WritePropertyName("coveContext");
                contextEl.WriteTo(writer);
            }
            writer.WriteEndObject();
            writer.Flush();
        }
        var bytes = buffer.ToArray();
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.Close();
    }

    private void RespondResolve(HttpListenerContext ctx)
    {
        var adapter = ctx.Request.Headers["X-Cove-Adapter"];
        var eventName = ctx.Request.Headers["X-Cove-Event"];
        var nookId = ctx.Request.Headers["X-Cove-Nook-Id"];

        if (Injector is null || adapter is null || eventName is null)
        {
            _logger?.HookResolveMissingParams(adapter ?? "", eventName ?? "");
            ctx.Response.StatusCode = 400;
            ctx.Response.Close();
            return;
        }

        JsonElement context = JsonDocument.Parse("{}").RootElement.Clone();
        if (ctx.Request.HasEntityBody && ctx.Request.ContentLength64 > 0)
        {
            using var reader = new StreamReader(ctx.Request.InputStream);
            var body = reader.ReadToEnd();
            if (!string.IsNullOrEmpty(body))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    context = doc.RootElement.Clone();
                }
                catch (JsonException ex)
                {
                    _logger?.HookPayloadInvalid(adapter, eventName, ex.Message);
                }
            }
        }
        else if (Aggregator is not null)
        {
            var ambient = Aggregator.Get(eventName, nookId) ?? Aggregator.Get("session", nookId);
            if (ambient is { } amb)
                context = amb;
        }

        var rendered = Injector.Render(adapter, eventName, context);
        if (nookId is not null)
            ctx.Response.Headers["X-Cove-Nook-Id"] = nookId;
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "application/json";
        var bytes = System.Text.Encoding.UTF8.GetBytes(rendered);
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.Close();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        try { _listener?.Stop(); _listener?.Close(); } catch { }
        try { _listenTask?.Wait(2000); } catch { }
        _cts?.Dispose();
    }
}
