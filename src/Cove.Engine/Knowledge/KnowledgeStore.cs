using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Protocol;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed class NoteStore
{
    private readonly string _dir;

    public NoteStore(string dataDir)
    {
        _dir = System.IO.Path.Combine(dataDir, "notes");
        System.IO.Directory.CreateDirectory(_dir);
    }

    public Note Create(Note note)
    {
        if (string.IsNullOrEmpty(note.Id))
            note = note with { Id = System.Guid.NewGuid().ToString("N") };
        Write(note);
        return note;
    }

    public Note? Get(string id)
    {
        var path = System.IO.Path.Combine(_dir, id + ".json");
        if (!System.IO.File.Exists(path)) return null;
        return JsonSerializer.Deserialize(System.IO.File.ReadAllText(path), KnowledgeJsonContext.Default.Note);
    }

    public System.Collections.Generic.IReadOnlyList<Note> ListByWorkspace(string workspaceId)
    {
        var result = new System.Collections.Generic.List<Note>();
        foreach (var file in System.IO.Directory.EnumerateFiles(_dir, "*.json"))
        {
            var note = JsonSerializer.Deserialize(System.IO.File.ReadAllText(file), KnowledgeJsonContext.Default.Note);
            if (note is { } n && n.WorkspaceId == workspaceId)
                result.Add(n);
        }
        return result;
    }

    public void Update(string id, System.Func<Note, Note> update)
    {
        var existing = Get(id);
        if (existing is null) return;
        Write(update(existing));
    }

    public void Delete(string id)
    {
        var path = System.IO.Path.Combine(_dir, id + ".json");
        if (System.IO.File.Exists(path))
            System.IO.File.Delete(path);
    }

    private void Write(Note note)
    {
        var path = System.IO.Path.Combine(_dir, note.Id + ".json");
        System.IO.File.WriteAllText(path, JsonSerializer.Serialize(note, KnowledgeJsonContext.Default.Note));
    }
}

public sealed class TimelineStore
{
    private readonly string _dbPath;
    private readonly ILogger _logger;

    public TimelineStore(string dataDir, ILogger logger)
    {
        _dbPath = System.IO.Path.Combine(dataDir, "timeline.db");
        _logger = logger;
    }

    public TimelineEntry Append(TimelineEntry entry)
    {
        var created = entry with
        {
            Id = string.IsNullOrEmpty(entry.Id) ? System.Guid.NewGuid().ToString("N") : entry.Id,
            Timestamp = entry.Timestamp == default ? System.DateTimeOffset.UtcNow : entry.Timestamp,
        };

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO timeline (id, workspace_id, kind, scope, title, body, metadata_json, tags_json, pane_id, created_at, backfill_key)
            VALUES (@id, @ws, @kind, @scope, @title, @body, @meta, @tags, @pane, @ts, @bf)
            ON CONFLICT(backfill_key) WHERE backfill_key IS NOT NULL DO NOTHING;
            """;
        cmd.Parameters.AddWithValue("@id", created.Id);
        cmd.Parameters.AddWithValue("@ws", created.WorkspaceId);
        cmd.Parameters.AddWithValue("@kind", created.Kind);
        cmd.Parameters.AddWithValue("@scope", (object?)created.Scope ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@title", (object?)created.Summary ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@body", System.DBNull.Value);
        cmd.Parameters.AddWithValue("@meta", (object?)created.JsonPayload ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@tags", System.DBNull.Value);
        cmd.Parameters.AddWithValue("@pane", System.DBNull.Value);
        cmd.Parameters.AddWithValue("@ts", created.Timestamp.ToString("o"));
        cmd.Parameters.AddWithValue("@bf", (object?)ComputeBackfillKey(created) ?? System.DBNull.Value);
        cmd.ExecuteNonQuery();
        _logger.LogWarning("timeline: appended {kind} for {ws}", created.Kind, created.WorkspaceId);
        return created;
    }

    public System.Collections.Generic.IReadOnlyList<TimelineEntry> ListByWorkspace(string workspaceId, int limit = 100)
    {
        var result = new System.Collections.Generic.List<TimelineEntry>();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, workspace_id, kind, scope, title, body, metadata_json, created_at FROM timeline WHERE workspace_id = @ws ORDER BY created_at DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@ws", workspaceId);
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new TimelineEntry
            {
                Id = reader.GetString(0),
                WorkspaceId = reader.GetString(1),
                Kind = reader.GetString(2),
                Source = "timeline.db",
                Scope = reader.IsDBNull(3) ? null : reader.GetString(3),
                Summary = reader.IsDBNull(4) ? null : reader.GetString(4),
                JsonPayload = reader.IsDBNull(6) ? null : reader.GetString(6),
                Timestamp = System.DateTimeOffset.Parse(reader.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind),
            });
        }
        return result;
    }

    public System.Collections.Generic.IReadOnlyList<TimelineEntry> Search(string workspaceId, string query, int limit = 20)
    {
        var result = new System.Collections.Generic.List<TimelineEntry>();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT t.id, t.workspace_id, t.kind, t.scope, t.title, t.body, t.metadata_json, t.created_at
            FROM timeline_fts f
            JOIN timeline t ON t.rowid = f.rowid
            WHERE t.workspace_id = @ws AND timeline_fts MATCH @query
            ORDER BY rank
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@ws", workspaceId);
        cmd.Parameters.AddWithValue("@query", query);
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new TimelineEntry
            {
                Id = reader.GetString(0),
                WorkspaceId = reader.GetString(1),
                Kind = reader.GetString(2),
                Source = "timeline.db",
                Scope = reader.IsDBNull(3) ? null : reader.GetString(3),
                Summary = reader.IsDBNull(4) ? null : reader.GetString(4),
                JsonPayload = reader.IsDBNull(6) ? null : reader.GetString(6),
                Timestamp = System.DateTimeOffset.Parse(reader.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind),
            });
        }
        return result;
    }

    private static string? ComputeBackfillKey(TimelineEntry entry)
    {
        if (entry.Source == "manual") return null;
        return $"{entry.Source}:{entry.Kind}:{entry.WorkspaceId}:{entry.Timestamp.ToUnixTimeSeconds()}";
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Note))]
[JsonSerializable(typeof(TimelineEntry))]
[JsonSerializable(typeof(System.Collections.Generic.List<Note>))]
[JsonSerializable(typeof(System.Collections.Generic.List<TimelineEntry>))]
public sealed partial class KnowledgeJsonContext : JsonSerializerContext { }
