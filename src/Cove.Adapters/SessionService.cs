using System.Text.Json;
using System.Text.Json.Serialization;

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
    private readonly Dictionary<(string adapter, string cwd), (List<RecentSession> sessions, DateTimeOffset at)> _cache = new();
    private readonly Dictionary<string, int> _schemaVersions = new();

    public SessionService(MethodRunner runner, TimeSpan? cacheTtl = null)
    {
        _runner = runner;
        _cacheTtl = cacheTtl ?? TimeSpan.FromSeconds(2);
    }

    public async Task<List<RecentSession>> ListRecentSessionsAsync(string adapterDir, string cwd, CancellationToken ct = default)
    {
        var key = (adapterDir, cwd);
        if (_cache.TryGetValue(key, out var entry) && DateTimeOffset.UtcNow - entry.at < _cacheTtl)
            return entry.sessions;

        var result = await _runner.RunAsync(adapterDir, "list_recent_sessions.sh", [cwd], TimeSpan.FromMilliseconds(50), null, ct);

        List<RecentSession> sessions;
        if (result.Ok && result.Json is { } json)
        {
            try
            {
                sessions = json.GetProperty("sessions").Deserialize(AdaptersJsonContext.Default.ListRecentSession) ?? new();
                sessions.Sort((a, b) => (b.LastActive ?? DateTimeOffset.MinValue).CompareTo(a.LastActive ?? DateTimeOffset.MinValue));
            }
            catch (Exception)
            {
                sessions = new();
            }
        }
        else
        {
            sessions = new();
        }

        _cache[key] = (sessions, DateTimeOffset.UtcNow);
        return sessions;
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

        var events = new List<CanonicalEvent>();
        if (!result.Ok)
            return events;

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
            catch (JsonException) { }
        }

        return events;
    }

    public bool CheckSchemaVersion(string adapter, int oldSchema, int newSchema)
    {
        _schemaVersions[adapter] = newSchema;
        return oldSchema != newSchema;
    }
}
