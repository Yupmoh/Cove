using System.Text.Json;
using Cove.Persistence;
using Cove.Protocol;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed record SessionCorpusEntry(string Id, string BayId, string Adapter, string StartedAt, string? EndedAt, string? ExtractorVersion);

public sealed class SessionCorpusIndexer
{
    private readonly SqliteConnectionFactory _database;
    private readonly ILogger _logger;

    public SessionCorpusIndexer(
        string dataDir,
        ILogger logger,
        SqliteConnectionFactory? database = null)
    {
        var databasePath = System.IO.Path.Combine(dataDir, "fts", "index.db");
        _logger = logger;
        _database = database ?? new SqliteConnectionFactory(databasePath, logger);
    }

    public string IndexSession(string bayId, string adapter, string startedAt, string corpus, string extractorVersion)
    {
        var sessionId = System.Guid.NewGuid().ToString("N");

        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sessions (id, bay_id, adapter, corpus, started_at, ended_at, extractor_version)
            VALUES (@id, @ws, @adapter, @corpus, @started, NULL, @ver);
            """;
        cmd.Parameters.AddWithValue("@id", sessionId);
        cmd.Parameters.AddWithValue("@ws", bayId);
        cmd.Parameters.AddWithValue("@adapter", adapter);
        cmd.Parameters.AddWithValue("@corpus", corpus);
        cmd.Parameters.AddWithValue("@started", startedAt);
        cmd.Parameters.AddWithValue("@ver", extractorVersion);
        cmd.ExecuteNonQuery();

        _logger.LogWarning("vault: indexed session {id} ({adapter}) in {ws} with extractor v{ver}", sessionId, adapter, bayId, extractorVersion);
        return sessionId;
    }

    public void SetSessionEnded(string sessionId, string endedAt)
    {
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE sessions SET ended_at = @ended WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", sessionId);
        cmd.Parameters.AddWithValue("@ended", endedAt);
        cmd.ExecuteNonQuery();
    }

    public System.Collections.Generic.IReadOnlyList<SessionCorpusEntry> SearchSessions(string bayId, string query, int limit = 20)
    {
        var result = new System.Collections.Generic.List<SessionCorpusEntry>();
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT s.id, s.bay_id, s.adapter, s.started_at, s.ended_at, s.extractor_version
            FROM sessions_fts f
            JOIN sessions s ON s.rowid = f.rowid
            WHERE s.bay_id = @ws AND sessions_fts MATCH @query
            ORDER BY rank
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@ws", bayId);
        cmd.Parameters.AddWithValue("@query", query);
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(ReadEntry(reader));
        return result;
    }

    public System.Collections.Generic.IReadOnlyList<SessionCorpusEntry> SearchSessionsTrigram(string bayId, string query, int limit = 20)
    {
        var result = new System.Collections.Generic.List<SessionCorpusEntry>();
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT s.id, s.bay_id, s.adapter, s.started_at, s.ended_at, s.extractor_version
            FROM sessions_fts_trigram f
            JOIN sessions s ON s.rowid = f.rowid
            WHERE s.bay_id = @ws AND sessions_fts_trigram MATCH @query
            ORDER BY rank
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@ws", bayId);
        cmd.Parameters.AddWithValue("@query", query);
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(ReadEntry(reader));
        return result;
    }

    public int ReindexIfVersionChanged(string bayId, string newVersion)
    {
        using var conn = _database.Open();


        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT DISTINCT extractor_version FROM sessions WHERE bay_id = @ws AND extractor_version != @ver";
        checkCmd.Parameters.AddWithValue("@ws", bayId);
        checkCmd.Parameters.AddWithValue("@ver", newVersion);
        var staleVersions = new System.Collections.Generic.List<string>();
        using (var reader = checkCmd.ExecuteReader())
        {
            while (reader.Read())
                staleVersions.Add(reader.GetString(0));
        }

        if (staleVersions.Count == 0) return 0;

        int reindexed = 0;
        foreach (var oldVersion in staleVersions)
        {
            using var updateCmd = conn.CreateCommand();
            updateCmd.CommandText = "UPDATE sessions SET extractor_version = @newVer WHERE bay_id = @ws AND extractor_version = @oldVer";
            updateCmd.Parameters.AddWithValue("@ws", bayId);
            updateCmd.Parameters.AddWithValue("@newVer", newVersion);
            updateCmd.Parameters.AddWithValue("@oldVer", oldVersion);
            reindexed += updateCmd.ExecuteNonQuery();
        }

        _logger.LogWarning("vault: reindexed {count} sessions from {oldVersions} → v{newVer} in {ws}", reindexed, string.Join(",", staleVersions), newVersion, bayId);
        return reindexed;
    }

    public System.Collections.Generic.IReadOnlyList<SessionCorpusEntry> ListSessions(string bayId, int limit = 50)
    {
        var result = new System.Collections.Generic.List<SessionCorpusEntry>();
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, bay_id, adapter, started_at, ended_at, extractor_version FROM sessions WHERE bay_id = @ws ORDER BY started_at DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@ws", bayId);
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(ReadEntry(reader));
        return result;
    }

    private static SessionCorpusEntry ReadEntry(SqliteDataReader reader)
    {
        return new SessionCorpusEntry(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5)
        );
    }
}
