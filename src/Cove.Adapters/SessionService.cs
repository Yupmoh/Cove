using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Cove.Adapters;

public sealed record RecentSession(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("cwd")] string? Cwd,
    [property: JsonPropertyName("lastActive")] DateTimeOffset? LastActive);

public sealed record CanonicalEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("sessionId")] string? SessionId,
    [property: JsonPropertyName("cwd")] string? Cwd,
    [property: JsonPropertyName("timestamp")] DateTimeOffset? Timestamp,
    [property: JsonPropertyName("role")] string? Role,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("raw")] JsonElement? Raw);

public sealed class SessionService
{
    private readonly MethodRunner _runner;
    private readonly TimeSpan _cacheTtl;
    private readonly TimeSpan _listTimeout;
    private readonly ILogger? _logger;
    private readonly Action? _backgroundRefreshCompleted;
    private readonly TimeProvider _time;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(string adapter, string cwd), (List<RecentSession> sessions, DateTimeOffset at)> _cache = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(string adapter, string cwd), bool> _refreshing = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _schemaVersions = new();

    public SessionService(
        MethodRunner runner,
        TimeSpan? cacheTtl = null,
        TimeSpan? listTimeout = null,
        ILogger? logger = null,
        Action? backgroundRefreshCompleted = null,
        TimeProvider? time = null)
    {
        _runner = runner;
        _cacheTtl = cacheTtl ?? TimeSpan.FromSeconds(2);
        _listTimeout = listTimeout ?? TimeSpan.FromSeconds(3);
        _logger = logger;
        _backgroundRefreshCompleted = backgroundRefreshCompleted;
        _time = time ?? TimeProvider.System;
    }

    private static string AdapterNameOf(string adapterDir)
        => Path.GetFileName(adapterDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    public async Task<List<RecentSession>> ListRecentSessionsAsync(string adapterDir, string cwd, CancellationToken ct = default)
    {
        var adapter = AdapterNameOf(adapterDir);
        var key = (adapterDir, cwd);
        if (_cache.TryGetValue(key, out var entry))
        {
            _logger?.SessionListCacheHit(adapter, cwd, entry.sessions.Count);
            if (_time.GetUtcNow() - entry.at >= _cacheTtl)
                RefreshInBackground(adapter, adapterDir, cwd, key);
            return entry.sessions;
        }

        var (ok, sessions) = await FetchSessionsAsync(adapter, adapterDir, cwd, ct).ConfigureAwait(false);
        if (ok)
            _cache[key] = (sessions, _time.GetUtcNow());
        return sessions;
    }

    private void RefreshInBackground(string adapter, string adapterDir, string cwd, (string adapter, string cwd) key)
    {
        if (!_refreshing.TryAdd(key, true))
            return;
        _ = Task.Run(async () =>
        {
            try
            {
                var (ok, sessions) = await FetchSessionsAsync(adapter, adapterDir, cwd, CancellationToken.None).ConfigureAwait(false);
                if (ok)
                    _cache[key] = (sessions, _time.GetUtcNow());
            }
            catch (Exception ex)
            {
                _logger?.SessionListParseFailed(adapter, ex.Message);
            }
            finally
            {
                _refreshing.TryRemove(key, out _);
                _backgroundRefreshCompleted?.Invoke();
            }
        });
    }

    private async Task<(bool Ok, List<RecentSession> Sessions)> FetchSessionsAsync(string adapter, string adapterDir, string cwd, CancellationToken ct)
    {
        var result = await _runner.RunAsync(adapterDir, "list_recent_sessions.sh", [cwd], _listTimeout, null, ct).ConfigureAwait(false);

        List<RecentSession> sessions;
        var ok = false;
        if (result.Ok && result.Json is { } json)
        {
            try
            {
                sessions = json.GetProperty("sessions").Deserialize(AdaptersJsonContext.Default.ListRecentSession) ?? new();
                sessions.Sort((a, b) => (b.LastActive ?? DateTimeOffset.MinValue).CompareTo(a.LastActive ?? DateTimeOffset.MinValue));
                ok = true;
            }
            catch (Exception ex)
            {
                _logger?.SessionListParseFailed(adapter, ex.Message);
                sessions = new();
            }
        }
        else
        {
            if (!result.Ok)
                _logger?.SessionListFailed(adapter, cwd, result.ExitCode);
            sessions = new();
        }

        _logger?.SessionListCompleted(adapter, cwd, sessions.Count);
        return (ok, sessions);
    }

    public async Task<List<CanonicalEvent>> ExtractSessionAsync(
        string adapterDir,
        string script,
        string sessionId,
        string cwd,
        string depth,
        CancellationToken ct = default)
    {
        var result = await _runner.RunAsync(
            adapterDir,
            script,
            ["--session-id", sessionId, "--cwd", cwd, "--depth", depth],
            TimeSpan.FromSeconds(10),
            null,
            ct);

        var adapter = AdapterNameOf(adapterDir);
        var events = new List<CanonicalEvent>();
        if (!result.Ok)
        {
            _logger?.SessionExtractFailed(adapter, script, result.ExitCode);
            return events;
        }

        foreach (var line in result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;
                if (root.TryGetProperty("type", out var typeEl))
                {
                    events.Add(new CanonicalEvent(
                        Type: typeEl.GetString() ?? "unknown",
                        SessionId: root.TryGetProperty("sessionId", out var s) ? s.GetString() : null,
                        Cwd: root.TryGetProperty("cwd", out var c) ? c.GetString() : null,
                        Timestamp: root.TryGetProperty("timestamp", out var t) && t.TryGetDateTimeOffset(out var ts) ? ts : null,
                        Role: root.TryGetProperty("role", out var r) ? r.GetString() : null,
                        Content: root.TryGetProperty("content", out var ct2) ? ct2.GetString() : null,
                        Raw: root.Clone()));
                }
            }
            catch (JsonException ex) { _logger?.SessionEventParseFailed(adapter, ex.Message); }
        }

        return events;
    }

    public bool CheckSchemaVersion(string adapter, int oldSchema, int newSchema)
    {
        _schemaVersions[adapter] = newSchema;
        return oldSchema != newSchema;
    }
}
