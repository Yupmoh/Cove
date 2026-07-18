using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using Cove.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cove.Gui;

public sealed class LoopbackServer : IAsyncDisposable
{
    public const int DefaultPort = 7420;
    private const string WsGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private readonly string _webRoot;
    private readonly Func<CancellationToken, Task<Stream>> _dial;
    private readonly string _clientVersion;
    private readonly string _channel;
    private readonly TcpListener _listener;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();
    public int Port { get; private set; }

    public LoopbackServer(string webRoot, Func<CancellationToken, Task<Stream>> dial, string clientVersion, string channel, int port = DefaultPort)
        : this(webRoot, dial, clientVersion, channel, NullLogger.Instance, port) { }

    public LoopbackServer(string webRoot, Func<CancellationToken, Task<Stream>> dial, string clientVersion, string channel, ILogger logger, int port = DefaultPort)
    {
        _webRoot = webRoot;
        _dial = dial;
        _clientVersion = clientVersion;
        _channel = channel;
        _logger = logger;
        _listener = new TcpListener(IPAddress.Loopback, port);
        Port = port;
    }

    public void Start()
    {
        try { _listener.Start(); }
        catch (SocketException ex) { throw new InvalidOperationException($"loopback port {Port} unavailable", ex); }
        Port = ((IPEndPoint)_listener.LocalEndpoint!).Port;
        _ = Task.Run(AcceptLoopAsync);
        _logger.LoopbackServerStarted(Port);
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(_cts.Token); }
            catch (OperationCanceledException) { break; }
            _ = Task.Run(() => HandleAsync(client));
        }
    }

    public static string ComputeAcceptKey(string secWebSocketKey)
        => Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(secWebSocketKey + WsGuid)));

    private async Task HandleAsync(TcpClient client)
    {
        using (client)
        {
            var stream = client.GetStream();
            try
            {
                var req = await ReadRequestAsync(stream, _cts.Token);
                if (req is null) return;
                var (target, headers) = req.Value;
                var path = target.Split('?', 2)[0];

                if (headers.TryGetValue("upgrade", out var up) && up.Equals("websocket", StringComparison.OrdinalIgnoreCase) && path == "/pty")
                {
                    var accept = ComputeAcceptKey(headers["sec-websocket-key"]);
                    var resp = $"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(resp), _cts.Token);
                    var (nook, since) = ParsePtyQuery(target);
                    var ws = WebSocket.CreateFromStream(stream, isServer: true, subProtocol: null, keepAliveInterval: TimeSpan.FromSeconds(30));
                    await PtyWsHandler.RunAsync(ws, _dial, _clientVersion, _channel, nook, since, _logger, _cts.Token);
                    return;
                }

                if (path == "/media")
                {
                    await ServeMediaAsync(stream, target, headers, _cts.Token);
                    return;
                }

                await ServeStaticAsync(stream, path, _cts.Token);
            }
            catch (Exception ex) { _logger.LoopbackConnectionHandlerFailed(ex.Message); }
        }
    }

    private async Task ServeStaticAsync(Stream stream, string path, CancellationToken ct)
    {
        var rel = path == "/" ? "index.html" : path.TrimStart('/');
        if (!Path.HasExtension(rel)) rel = rel.TrimEnd('/') + "/index.html";
        var full = Path.GetFullPath(Path.Combine(_webRoot, rel));
        if (!PathContainment.IsContained(_webRoot, full) || !File.Exists(full))
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), ct);
            return;
        }
        var body = await File.ReadAllBytesAsync(full, ct);
        var header = $"HTTP/1.1 200 OK\r\nContent-Type: {ContentType(full)}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(header), ct);
        await stream.WriteAsync(body, ct);
    }

    private static string ContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".js" or ".mjs" => "text/javascript; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        ".map" => "application/json",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".woff2" => "font/woff2",
        ".wasm" => "application/wasm",
        ".pdf" => "application/pdf",
        ".mp4" or ".m4v" => "video/mp4",
        ".webm" => "video/webm",
        ".ogg" or ".ogv" => "video/ogg",
        ".mov" => "video/quicktime",
        _ => "application/octet-stream",
    };

    private async Task ServeMediaAsync(Stream stream, string target, Dictionary<string, string> headers, CancellationToken ct)
    {
        var filePath = ParseMediaPath(target);
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            _logger.LoopbackMediaNotFound(filePath);
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), ct);
            return;
        }

        var length = new FileInfo(filePath).Length;
        headers.TryGetValue("range", out var rangeHeader);
        var range = MediaRange.Resolve(rangeHeader, length);

        if (range.StatusCode == 416)
        {
            var invalid = $"HTTP/1.1 416 Range Not Satisfiable\r\nContent-Range: bytes */{length}\r\nAccept-Ranges: bytes\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(invalid), ct);
            return;
        }

        var statusLine = range.IsPartial ? "206 Partial Content" : "200 OK";
        var head = new StringBuilder();
        head.Append($"HTTP/1.1 {statusLine}\r\n");
        head.Append($"Content-Type: {ContentType(filePath)}\r\n");
        head.Append("Accept-Ranges: bytes\r\n");
        head.Append($"Content-Length: {range.Length}\r\n");
        if (range.IsPartial)
            head.Append($"Content-Range: bytes {range.Start}-{range.End}/{range.TotalLength}\r\n");
        head.Append("Connection: close\r\n\r\n");
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head.ToString()), ct);

        await using var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        file.Seek(range.Start, SeekOrigin.Begin);
        var remaining = range.Length;
        var buffer = new byte[81920];
        while (remaining > 0)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var read = await file.ReadAsync(buffer.AsMemory(0, toRead), ct);
            if (read == 0) break;
            await stream.WriteAsync(buffer.AsMemory(0, read), ct);
            remaining -= read;
        }
    }

    private static string ParseMediaPath(string target)
    {
        var q = target.Contains('?') ? target[(target.IndexOf('?') + 1)..] : "";
        foreach (var kv in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = kv.Split('=', 2);
            if (eq.Length == 2 && eq[0] == "path")
                return Uri.UnescapeDataString(eq[1]);
        }
        return "";
    }

    private static (string nook, ulong since) ParsePtyQuery(string target)
    {
        var q = target.Contains('?') ? target[(target.IndexOf('?') + 1)..] : "";
        string nook = ""; ulong since = 0;
        foreach (var kv in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = kv.Split('=', 2);
            if (eq.Length != 2) continue;
            if (eq[0] == "nook") nook = Uri.UnescapeDataString(eq[1]);
            else if (eq[0] == "since") _ = ulong.TryParse(eq[1], out since);
        }
        return (nook, since);
    }

    private static async Task<(string target, Dictionary<string, string> headers)?> ReadRequestAsync(Stream stream, CancellationToken ct)
    {
        var buf = new List<byte>(1024);
        var one = new byte[1];
        while (buf.Count < 16384)
        {
            var n = await stream.ReadAsync(one, ct);
            if (n == 0) return null;
            buf.Add(one[0]);
            if (buf.Count >= 4 && buf[^4] == (byte)'\r' && buf[^3] == (byte)'\n' && buf[^2] == (byte)'\r' && buf[^1] == (byte)'\n') break;
        }
        var text = Encoding.ASCII.GetString(buf.ToArray());
        var lines = text.Split("\r\n");
        var parts = lines[0].Split(' ');
        if (parts.Length < 2) return null;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < lines.Length; i++)
        {
            var idx = lines[i].IndexOf(':');
            if (idx <= 0) continue;
            headers[lines[i][..idx].Trim()] = lines[i][(idx + 1)..].Trim();
        }
        return (parts[1], headers);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _listener.Stop();
    }
}
