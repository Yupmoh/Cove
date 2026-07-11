using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Lsp;

public sealed record LspConfigEntry(string[] Languages, string Command, string[] Args);

public sealed record LspServerStatus(string Language, string Command, bool Running, bool Available);

public sealed class LspService : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, LspServerConfig> _serversByLanguage = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LspServer> _runningServers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unavailableServers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _warnedLanguages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _openDocumentVersions = new(StringComparer.Ordinal);
    private readonly Dictionary<LspServer, PublishedDiagnosticsCache> _publishedDiagnostics = new();
    private readonly System.Threading.SemaphoreSlim _gate = new(1, 1);

    private sealed class PublishedDiagnosticsCache
    {
        public readonly object Lock = new();
        public readonly Dictionary<string, (long Seq, JsonElement Diagnostics)> ByUri = new(StringComparer.Ordinal);
        public long NextSeq;
    }

    private static readonly LspConfigEntry[] BuiltInEntries =
    [
        new(["typescript", "javascript", "typescriptreact", "javascriptreact"], "typescript-language-server", ["--stdio"]),
        new(["json"], "vscode-json-language-server", ["--stdio"]),
        new(["css"], "vscode-css-language-server", ["--stdio"]),
        new(["html"], "vscode-html-language-server", ["--stdio"]),
    ];

    public LspService(ILogger logger, IReadOnlyList<LspConfigEntry>? userEntries = null)
    {
        _logger = logger;
        foreach (var entry in BuiltInEntries)
            Register(entry);
        if (userEntries is not null)
            foreach (var entry in userEntries)
                Register(entry);
    }

    private void Register(LspConfigEntry entry)
    {
        var config = new LspServerConfig(entry.Command, entry.Args, entry.Languages);
        foreach (var language in entry.Languages)
            _serversByLanguage[language] = config;
    }

    public LspServerConfig? ResolveServerFor(string languageId)
    {
        if (_serversByLanguage.TryGetValue(languageId, out var config))
            return config;
        if (_warnedLanguages.Add(languageId))
            _logger.LogWarning("lsp: no server configured for language {Language}", languageId);
        return null;
    }

    public static string? DetectLanguage(string filePath)
    {
        var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".ts" or ".mts" or ".cts" => "typescript",
            ".tsx" => "typescriptreact",
            ".js" or ".mjs" or ".cjs" => "javascript",
            ".jsx" => "javascriptreact",
            ".json" => "json",
            ".css" => "css",
            ".html" or ".htm" => "html",
            _ => null,
        };
    }

    public IReadOnlyList<LspServerStatus> GetStatuses()
    {
        var statuses = new List<LspServerStatus>();
        foreach (var (language, config) in _serversByLanguage)
        {
            var running = false;
            lock (_runningServers)
            {
                foreach (var (key, server) in _runningServers)
                    if (key.StartsWith(config.Command + " ", StringComparison.Ordinal) && server.IsRunning)
                        running = true;
            }
            bool available;
            lock (_unavailableServers)
                available = !_unavailableServers.Contains(config.Command);
            statuses.Add(new LspServerStatus(language, config.Command, running, available));
        }
        statuses.Sort((a, b) => string.CompareOrdinal(a.Language, b.Language));
        return statuses;
    }

    public async Task<LspServer?> GetOrStartServerAsync(string languageId, string rootDir, CancellationToken ct = default)
    {
        if (ResolveServerFor(languageId) is not { } config)
            return null;
        lock (_unavailableServers)
            if (_unavailableServers.Contains(config.Command))
                return null;

        var key = config.Command + " " + rootDir;
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            lock (_runningServers)
                if (_runningServers.TryGetValue(key, out var existing) && existing.IsRunning)
                    return existing;

            var rooted = config with { RootUri = "file://" + rootDir };
            var server = new LspServer(rooted, _logger);
            var started = false;
            try
            {
                started = await server.StartAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "lsp: start threw for {Command}", config.Command);
            }
            if (!started)
            {
                _logger.LogWarning("lsp: server {Command} unavailable, marking language {Language} disabled", config.Command, languageId);
                lock (_unavailableServers)
                    _unavailableServers.Add(config.Command);
                await server.DisposeAsync().ConfigureAwait(false);
                return null;
            }
            var cache = new PublishedDiagnosticsCache();
            lock (_runningServers)
                _runningServers[key] = server;
            lock (_publishedDiagnostics)
                _publishedDiagnostics[server] = cache;
            _ = PumpNotificationsAsync(server, cache);
            return server;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task PumpNotificationsAsync(LspServer server, PublishedDiagnosticsCache cache)
    {
        try
        {
            await foreach (var notification in server.Notifications.ConfigureAwait(false))
            {
                if (notification.Method != "textDocument/publishDiagnostics" || notification.Params is not { } p)
                    continue;
                if (!p.TryGetProperty("uri", out var uriEl) || uriEl.ValueKind != JsonValueKind.String)
                    continue;
                if (!p.TryGetProperty("diagnostics", out var diagnostics) || diagnostics.ValueKind != JsonValueKind.Array)
                    continue;
                var uri = uriEl.GetString() ?? "";
                lock (cache.Lock)
                    cache.ByUri[uri] = (++cache.NextSeq, diagnostics);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "lsp: notification pump terminated unexpectedly");
        }
    }

    public long GetPublishedDiagnosticsSequence(LspServer server, string uri)
    {
        PublishedDiagnosticsCache? cache;
        lock (_publishedDiagnostics)
            _publishedDiagnostics.TryGetValue(server, out cache);
        if (cache is null) return 0;
        lock (cache.Lock)
            return cache.ByUri.TryGetValue(uri, out var entry) ? entry.Seq : 0;
    }

    public async Task<JsonElement?> WaitForPublishedDiagnosticsAsync(LspServer server, string uri, long afterSeq, TimeSpan timeout, CancellationToken ct = default)
    {
        PublishedDiagnosticsCache? cache;
        lock (_publishedDiagnostics)
            _publishedDiagnostics.TryGetValue(server, out cache);
        if (cache is null)
        {
            _logger.LogWarning("lsp: no diagnostics cache registered for server handling {Uri}", uri);
            return null;
        }
        var deadline = DateTime.UtcNow + timeout;
        DateTime? settleDeadline = null;
        JsonElement? latest = null;
        while (DateTime.UtcNow < deadline)
        {
            lock (cache.Lock)
            {
                if (cache.ByUri.TryGetValue(uri, out var entry) && entry.Seq > afterSeq)
                {
                    latest = entry.Diagnostics;
                    if (entry.Diagnostics.GetArrayLength() > 0)
                        return latest;
                    settleDeadline ??= DateTime.UtcNow.AddSeconds(2);
                }
            }
            if (settleDeadline is { } settle && DateTime.UtcNow >= settle)
                return latest;
            await Task.Delay(50, ct).ConfigureAwait(false);
        }
        return latest;
    }

    public void WarnUnknownFileLanguage(string filePath)
    {
        var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        if (_warnedLanguages.Add("ext:" + ext))
            _logger.LogWarning("lsp: no language mapping for extension {Extension} ({FilePath})", ext, filePath);
    }

    public void WarnRequestFailed(string method, string filePath, Exception ex)
        => _logger.LogWarning(ex, "lsp: {Method} failed for {FilePath}", method, filePath);

    public int NextDocumentVersion(string filePath, out bool alreadyOpen)
    {
        lock (_openDocumentVersions)
        {
            alreadyOpen = _openDocumentVersions.TryGetValue(filePath, out var version);
            var next = alreadyOpen ? version + 1 : 1;
            _openDocumentVersions[filePath] = next;
            return next;
        }
    }

    public async ValueTask DisposeAsync()
    {
        List<LspServer> servers;
        lock (_runningServers)
        {
            servers = new List<LspServer>(_runningServers.Values);
            _runningServers.Clear();
        }
        lock (_publishedDiagnostics)
            _publishedDiagnostics.Clear();
        foreach (var server in servers)
            await server.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
