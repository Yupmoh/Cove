using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Persistence;
using Cove.Protocol;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed class TimelineStore
{
    private readonly SqliteConnectionFactory _database;
    private readonly ILogger _logger;
    private readonly TimelineValidator _validator = new();

    public TimelineStore(
        string dataDir,
        ILogger logger,
        SqliteConnectionFactory? database = null)
    {
        var databasePath = System.IO.Path.Combine(dataDir, "timeline.db");
        _logger = logger;
        _database = database ?? new SqliteConnectionFactory(databasePath, logger);
    }

    public TimelineEntry Append(TimelineEntry entry)
    {
        _validator.Validate(entry);

        var created = entry with
        {
            Id = string.IsNullOrEmpty(entry.Id) ? System.Guid.NewGuid().ToString("N") : entry.Id,
            Timestamp = entry.Timestamp == default ? System.DateTimeOffset.UtcNow : entry.Timestamp,
        };

        var tagsJson = created.Tags is { } tags && tags.Count > 0
            ? JsonSerializer.Serialize(new System.Collections.Generic.List<string>(tags), KnowledgeJsonContext.Default.ListString)
            : null;

        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO timeline (id, bay_id, kind, scope, title, body, metadata_json, tags_json, nook_id, created_at, backfill_key)
            VALUES (@id, @ws, @kind, @scope, @title, @body, @meta, @tags, @nook, @ts, @bf)
            ON CONFLICT(backfill_key) WHERE backfill_key IS NOT NULL DO NOTHING;
            """;
        cmd.Parameters.AddWithValue("@id", created.Id);
        cmd.Parameters.AddWithValue("@ws", created.BayId);
        cmd.Parameters.AddWithValue("@kind", created.Kind);
        cmd.Parameters.AddWithValue("@scope", (object?)created.Scope ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@title", (object?)created.Summary ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@body", System.DBNull.Value);
        cmd.Parameters.AddWithValue("@meta", (object?)created.JsonPayload ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@tags", (object?)tagsJson ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@nook", System.DBNull.Value);
        cmd.Parameters.AddWithValue("@ts", created.Timestamp.ToString("o"));
        cmd.Parameters.AddWithValue("@bf", (object?)ComputeBackfillKey(created) ?? System.DBNull.Value);
        cmd.ExecuteNonQuery();
        _logger.LogWarning("timeline: appended {kind} for {ws}", created.Kind, created.BayId);
        return created;
    }

    public System.Collections.Generic.IReadOnlyList<TimelineEntry> ListByBay(string bayId, int limit = 100)
    {
        var result = new System.Collections.Generic.List<TimelineEntry>();
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, bay_id, kind, scope, title, body, metadata_json, tags_json, created_at FROM timeline WHERE bay_id = @ws ORDER BY created_at DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@ws", bayId);
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(ReadEntry(reader));
        }
        return result;
    }

    public System.Collections.Generic.IReadOnlyList<TimelineEntry> Search(string bayId, string query, int limit = 20)
    {
        var result = new System.Collections.Generic.List<TimelineEntry>();
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT t.id, t.bay_id, t.kind, t.scope, t.title, t.body, t.metadata_json, t.tags_json, t.created_at
            FROM timeline_fts f
            JOIN timeline t ON t.rowid = f.rowid
            WHERE t.bay_id = @ws AND timeline_fts MATCH @query
            ORDER BY rank
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@ws", bayId);
        cmd.Parameters.AddWithValue("@query", query);
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(ReadEntry(reader));
        }
        return result;
    }

    private static TimelineEntry ReadEntry(SqliteDataReader reader)
    {
        System.Collections.Generic.IReadOnlyList<string>? tags = null;
        if (!reader.IsDBNull(7))
        {
            var tagsJson = reader.GetString(7);
            if (!string.IsNullOrEmpty(tagsJson))
                tags = JsonSerializer.Deserialize(tagsJson, KnowledgeJsonContext.Default.ListString);
        }

        return new TimelineEntry
        {
            Id = reader.GetString(0),
            BayId = reader.GetString(1),
            Kind = reader.GetString(2),
            Source = "timeline.db",
            Scope = reader.IsDBNull(3) ? null : reader.GetString(3),
            Summary = reader.IsDBNull(4) ? null : reader.GetString(4),
            JsonPayload = reader.IsDBNull(6) ? null : reader.GetString(6),
            Tags = tags,
            Timestamp = System.DateTimeOffset.Parse(reader.GetString(8), null, System.Globalization.DateTimeStyles.RoundtripKind),
        };
    }

    private static string? ComputeBackfillKey(TimelineEntry entry)
    {
        if (entry.Source == "manual") return null;
        return $"{entry.Source}:{entry.Kind}:{entry.BayId}:{entry.Timestamp.ToUnixTimeSeconds()}";
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Note))]
[JsonSerializable(typeof(TimelineEntry))]
[JsonSerializable(typeof(System.Collections.Generic.List<Note>))]
[JsonSerializable(typeof(System.Collections.Generic.List<TimelineEntry>))]
[JsonSerializable(typeof(System.Collections.Generic.List<string>))]
public sealed partial class KnowledgeJsonContext : JsonSerializerContext { }
