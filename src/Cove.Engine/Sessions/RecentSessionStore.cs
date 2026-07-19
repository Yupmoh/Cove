using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Cove.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Sessions;

public sealed record RecentSession(
    string Adapter,
    string SessionId,
    string BayId,
    string Cwd,
    DateTimeOffset StartedAt);

public sealed class RecentSessionStore
{
    private readonly SqliteConnectionFactory _database;
    private readonly ILogger _logger;

    public RecentSessionStore(
        string dataDir,
        ILogger logger,
        SqliteConnectionFactory? database = null)
    {
        var databasePath = Path.Combine(dataDir, "sessions.db");
        _logger = logger;
        _database = database ?? new SqliteConnectionFactory(databasePath, logger);
        EnsureSchema();
    }

    public void RecordStart(string adapter, string sessionId, string bayId, string cwd, DateTimeOffset startedAt)
    {
        if (string.IsNullOrEmpty(adapter) || string.IsNullOrEmpty(sessionId))
        {
            _logger.LogWarning("recent session: skipped record with empty adapter or sessionId adapter={Adapter} sessionId={SessionId}", adapter, sessionId);
            return;
        }
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sessions (adapter, session_id, bay_id, cwd, started_at)
            VALUES (@adapter, @session, @ws, @cwd, @ts)
            """;
        cmd.Parameters.AddWithValue("@adapter", adapter);
        cmd.Parameters.AddWithValue("@session", sessionId);
        cmd.Parameters.AddWithValue("@ws", bayId ?? "");
        cmd.Parameters.AddWithValue("@cwd", cwd ?? "");
        cmd.Parameters.AddWithValue("@ts", startedAt.ToString("o"));
        cmd.ExecuteNonQuery();
        _logger.LogDebug("recent session recorded adapter={Adapter} sessionId={SessionId} bay={BayId}", adapter, sessionId, bayId ?? "");
    }

    public IReadOnlyList<RecentSession> Recent(string? adapter, int limit)
    {
        var result = new List<RecentSession>();
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        var filter = string.IsNullOrEmpty(adapter) ? "" : " WHERE adapter = @adapter";
        var cap = limit > 0 ? " LIMIT @limit" : "";
        cmd.CommandText = $"SELECT adapter, session_id, bay_id, cwd, started_at FROM sessions{filter} ORDER BY started_at DESC, rowid DESC{cap}";
        if (!string.IsNullOrEmpty(adapter))
            cmd.Parameters.AddWithValue("@adapter", adapter);
        if (limit > 0)
            cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(new RecentSession(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        return result;
    }

    public int PurgeAdapter(string adapter)
    {
        if (string.IsNullOrEmpty(adapter))
        {
            _logger.LogWarning("recent session purge: skipped empty adapter");
            return 0;
        }
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM sessions WHERE adapter = @adapter";
        cmd.Parameters.AddWithValue("@adapter", adapter);
        return cmd.ExecuteNonQuery();
    }

    private void EnsureSchema()
    {
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS sessions (
                rowid INTEGER PRIMARY KEY AUTOINCREMENT,
                adapter TEXT NOT NULL,
                session_id TEXT NOT NULL,
                bay_id TEXT NOT NULL DEFAULT '',
                cwd TEXT NOT NULL DEFAULT '',
                started_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_sessions_started ON sessions (started_at DESC);
            CREATE INDEX IF NOT EXISTS idx_sessions_adapter ON sessions (adapter);
            """;
        cmd.ExecuteNonQuery();
    }
}
