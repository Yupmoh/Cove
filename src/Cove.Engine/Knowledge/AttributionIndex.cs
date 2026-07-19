using Cove.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed record AttributionEntry(
    string Id,
    string SessionId,
    string ToolUseId,
    string FilePath,
    int StartLine,
    int EndLine,
    System.DateTimeOffset At);

public sealed class AttributionIndex
{
    private readonly SqliteConnectionFactory _database;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;

    public AttributionIndex(
        string dataDir,
        ILogger logger,
        TimeProvider? timeProvider = null,
        SqliteConnectionFactory? database = null)
    {
        var databasePath = System.IO.Path.Combine(dataDir, "attribution", "index.db");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(databasePath)!);
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _database = database ?? new SqliteConnectionFactory(databasePath, logger);
        EnsureSchema();
    }

    public AttributionEntry Record(string sessionId, string toolUseId, string filePath, int startLine, int endLine)
    {
        var id = System.Guid.NewGuid().ToString("N");
        var at = _timeProvider.GetUtcNow();
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO attribution (id, session_id, tool_use_id, file_path, start_line, end_line, at)
            VALUES (@id, @sid, @tid, @path, @sl, @el, @at)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@sid", sessionId);
        cmd.Parameters.AddWithValue("@tid", toolUseId);
        cmd.Parameters.AddWithValue("@path", filePath);
        cmd.Parameters.AddWithValue("@sl", startLine);
        cmd.Parameters.AddWithValue("@el", endLine);
        cmd.Parameters.AddWithValue("@at", at.ToString("o"));
        cmd.ExecuteNonQuery();

        return new AttributionEntry(id, sessionId, toolUseId, filePath, startLine, endLine, at);
    }

    public AttributionEntry? FindByLine(string filePath, int line)
    {
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, session_id, tool_use_id, file_path, start_line, end_line, at
            FROM attribution
            WHERE file_path = @path AND @line >= start_line AND @line <= end_line
            ORDER BY at DESC
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@path", filePath);
        cmd.Parameters.AddWithValue("@line", line);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
            return ReadEntry(reader);
        return null;
    }

    public System.Collections.Generic.IReadOnlyList<AttributionEntry> FindByRange(string filePath, int startLine, int endLine)
    {
        var result = new System.Collections.Generic.List<AttributionEntry>();
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, session_id, tool_use_id, file_path, start_line, end_line, at
            FROM attribution
            WHERE file_path = @path
              AND start_line <= @el AND end_line >= @sl
            ORDER BY start_line
            """;
        cmd.Parameters.AddWithValue("@path", filePath);
        cmd.Parameters.AddWithValue("@sl", startLine);
        cmd.Parameters.AddWithValue("@el", endLine);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(ReadEntry(reader));
        return result;
    }

    public System.Collections.Generic.IReadOnlyList<AttributionEntry> FindByToolUse(string toolUseId)
    {
        var result = new System.Collections.Generic.List<AttributionEntry>();
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, session_id, tool_use_id, file_path, start_line, end_line, at
            FROM attribution
            WHERE tool_use_id = @tid
            ORDER BY at
            """;
        cmd.Parameters.AddWithValue("@tid", toolUseId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(ReadEntry(reader));
        return result;
    }

    private static AttributionEntry ReadEntry(SqliteDataReader reader)
    {
        return new AttributionEntry(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            System.DateTimeOffset.Parse(reader.GetString(6), null, System.Globalization.DateTimeStyles.RoundtripKind)
        );
    }

    private void EnsureSchema()
    {
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS attribution (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                tool_use_id TEXT NOT NULL,
                file_path TEXT NOT NULL,
                start_line INTEGER NOT NULL,
                end_line INTEGER NOT NULL,
                at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_attribution_file_line ON attribution (file_path, start_line, end_line);
            CREATE INDEX IF NOT EXISTS idx_attribution_tool_use ON attribution (tool_use_id);
            CREATE INDEX IF NOT EXISTS idx_attribution_session ON attribution (session_id);
            """;
        cmd.ExecuteNonQuery();
    }
}
