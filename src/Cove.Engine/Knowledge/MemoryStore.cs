using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Protocol;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed class MemoryStore
{
    private readonly string _dbPath;
    private readonly string _factsDir;
    private readonly ILogger _logger;

    public MemoryStore(string dataDir, ILogger logger)
    {
        _dbPath = System.IO.Path.Combine(dataDir, "memory", "memory.db");
        _factsDir = System.IO.Path.Combine(dataDir, "memory", "facts");
        _logger = logger;
    }

    public Fact AddFact(Fact fact)
    {
        if (string.IsNullOrEmpty(fact.Id))
            fact = fact with { Id = System.Guid.NewGuid().ToString("N") };

        var now = System.DateTimeOffset.UtcNow;
        if (fact.CreatedAt == default) fact = fact with { CreatedAt = now };
        fact = fact with { UpdatedAt = now };

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO facts (id, bay_id, kind, content, confidence, access_count, audience, locus, file_path, superseded_by, created_at, updated_at)
            VALUES (@id, @ws, @kind, @content, @conf, 0, @aud, @locus, @fp, NULL, @created, @updated);
            """;
        cmd.Parameters.AddWithValue("@id", fact.Id);
        cmd.Parameters.AddWithValue("@ws", fact.BayId);
        cmd.Parameters.AddWithValue("@kind", fact.Kind);
        cmd.Parameters.AddWithValue("@content", fact.Content);
        cmd.Parameters.AddWithValue("@conf", fact.Confidence);
        cmd.Parameters.AddWithValue("@aud", (object?)fact.Audience ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@locus", (object?)fact.Locus ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@fp", (object?)fact.FilePath ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@created", fact.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@updated", fact.UpdatedAt.ToString("o"));
        cmd.ExecuteNonQuery();

        OffloadFactToFile(fact);

        _logger.LogWarning("memory: added fact {id} ({kind}) in {ws}", fact.Id, fact.Kind, fact.BayId);
        return fact;
    }

    public Fact? GetFact(string bayId, string id)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, bay_id, kind, content, confidence, access_count, audience, locus, file_path, superseded_by, created_at, updated_at FROM facts WHERE bay_id = @ws AND id = @id";
        cmd.Parameters.AddWithValue("@ws", bayId);
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return ReadFact(reader);
    }

    public System.Collections.Generic.IReadOnlyList<Fact> ListFacts(string bayId, string? kind = null, string? audience = null)
    {
        var result = new System.Collections.Generic.List<Fact>();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        var sql = "SELECT id, bay_id, kind, content, confidence, access_count, audience, locus, file_path, superseded_by, created_at, updated_at FROM facts WHERE bay_id = @ws";
        if (kind is not null) sql += " AND kind = @kind";
        if (audience is not null) sql += " AND (audience = @aud OR audience IS NULL)";
        sql += " AND superseded_by IS NULL ORDER BY confidence DESC, updated_at DESC";
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@ws", bayId);
        if (kind is not null) cmd.Parameters.AddWithValue("@kind", kind);
        if (audience is not null) cmd.Parameters.AddWithValue("@aud", audience);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(ReadFact(reader));
        return result;
    }

    public System.Collections.Generic.IReadOnlyList<Fact> SearchFacts(string bayId, string query, int limit = 20)
    {
        var result = new System.Collections.Generic.List<Fact>();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT f.id, f.bay_id, f.kind, f.content, f.confidence, f.access_count, f.audience, f.locus, f.file_path, f.superseded_by, f.created_at, f.updated_at
            FROM facts_fts ft
            JOIN facts f ON f.rowid = ft.rowid
            WHERE f.bay_id = @ws AND facts_fts MATCH @query
            ORDER BY rank
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@ws", bayId);
        cmd.Parameters.AddWithValue("@query", query);
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(ReadFact(reader));
        return result;
    }

    public Fact? Supersede(string bayId, string oldFactId, Fact newFact)
    {
        var oldFact = GetFact(bayId, oldFactId);
        if (oldFact is null)
        {
            _logger.LogWarning("memory: supersede failed — fact {id} not found in {ws}", oldFactId, bayId);
            return null;
        }

        var created = AddFact(newFact);

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE facts SET superseded_by = @newId, updated_at = @ts WHERE id = @oldId";
        cmd.Parameters.AddWithValue("@newId", created.Id);
        cmd.Parameters.AddWithValue("@oldId", oldFactId);
        cmd.Parameters.AddWithValue("@ts", System.DateTimeOffset.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();

        var chain = GetSupersedeChain(bayId, oldFactId);
        _logger.LogWarning("memory: superseded {oldId} → {newId} (chain length: {len})", oldFactId, created.Id, chain.Count);

        return created;
    }

    public System.Collections.Generic.IReadOnlyList<Fact> GetSupersedeChain(string bayId, string factId)
    {
        var chain = new System.Collections.Generic.List<Fact>();
        var current = GetFact(bayId, factId);
        if (current is null) return chain;
        chain.Add(current);

        while (current!.SupersededBy is { } nextId)
        {
            var next = GetFact(bayId, nextId);
            if (next is null) break;
            chain.Add(next);
            current = next;
        }

        return chain;
    }

    public void ReindexFromDisk(string bayId)
    {
        System.IO.Directory.CreateDirectory(_factsDir);
        var wsDir = System.IO.Path.Combine(_factsDir, bayId);
        if (!System.IO.Directory.Exists(wsDir)) return;

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var clearCmd = conn.CreateCommand();
        clearCmd.CommandText = "DELETE FROM facts WHERE bay_id = @ws";
        clearCmd.Parameters.AddWithValue("@ws", bayId);
        clearCmd.ExecuteNonQuery();

        int count = 0;
        foreach (var file in System.IO.Directory.GetFiles(wsDir, "*.json"))
        {
            try
            {
                var fact = JsonSerializer.Deserialize(System.IO.File.ReadAllText(file), CoveJsonContext.Default.Fact);
                if (fact is null) continue;
                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO facts (id, bay_id, kind, content, confidence, access_count, audience, locus, file_path, superseded_by, created_at, updated_at)
                    VALUES (@id, @ws, @kind, @content, @conf, @ac, @aud, @locus, @fp, @sb, @created, @updated);
                    """;
                cmd.Parameters.AddWithValue("@id", fact.Id);
                cmd.Parameters.AddWithValue("@ws", fact.BayId);
                cmd.Parameters.AddWithValue("@kind", fact.Kind);
                cmd.Parameters.AddWithValue("@content", fact.Content);
                cmd.Parameters.AddWithValue("@conf", fact.Confidence);
                cmd.Parameters.AddWithValue("@ac", fact.AccessCount);
                cmd.Parameters.AddWithValue("@aud", (object?)fact.Audience ?? System.DBNull.Value);
                cmd.Parameters.AddWithValue("@locus", (object?)fact.Locus ?? System.DBNull.Value);
                cmd.Parameters.AddWithValue("@fp", (object?)fact.FilePath ?? System.DBNull.Value);
                cmd.Parameters.AddWithValue("@sb", (object?)fact.SupersededBy ?? System.DBNull.Value);
                cmd.Parameters.AddWithValue("@created", fact.CreatedAt.ToString("o"));
                cmd.Parameters.AddWithValue("@updated", fact.UpdatedAt.ToString("o"));
                cmd.ExecuteNonQuery();
                count++;
            }
            catch (System.Exception ex)
            {
                _logger.LogWarning("memory: reindex failed for {file}: {err}", file, ex.Message);
            }
        }

        _logger.LogWarning("memory: reindexed {count} facts from disk for {ws}", count, bayId);
    }

    public void IncrementAccessCount(string bayId, string factId)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE facts SET access_count = access_count + 1, updated_at = @ts WHERE id = @id AND bay_id = @ws";
        cmd.Parameters.AddWithValue("@id", factId);
        cmd.Parameters.AddWithValue("@ws", bayId);
        cmd.Parameters.AddWithValue("@ts", System.DateTimeOffset.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    private void OffloadFactToFile(Fact fact)
    {
        var wsDir = System.IO.Path.Combine(_factsDir, fact.BayId);
        System.IO.Directory.CreateDirectory(wsDir);
        var path = System.IO.Path.Combine(wsDir, fact.Id + ".json");
        fact = fact with { FilePath = path };
        System.IO.File.WriteAllText(path, JsonSerializer.Serialize(fact, CoveJsonContext.Default.Fact));
    }


    private static Fact ReadFact(SqliteDataReader reader)
    {
        return new Fact
        {
            Id = reader.GetString(0),
            BayId = reader.GetString(1),
            Kind = reader.GetString(2),
            Content = reader.GetString(3),
            Confidence = reader.GetDouble(4),
            AccessCount = reader.GetInt32(5),
            Audience = reader.IsDBNull(6) ? null : reader.GetString(6),
            Locus = reader.IsDBNull(7) ? null : reader.GetString(7),
            FilePath = reader.IsDBNull(8) ? null : reader.GetString(8),
            SupersededBy = reader.IsDBNull(9) ? null : reader.GetString(9),
            CreatedAt = System.DateTimeOffset.Parse(reader.GetString(10), null, System.Globalization.DateTimeStyles.RoundtripKind),
            UpdatedAt = System.DateTimeOffset.Parse(reader.GetString(11), null, System.Globalization.DateTimeStyles.RoundtripKind),
        };
    }
}

