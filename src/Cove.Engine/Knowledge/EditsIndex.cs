using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed record EditRecord(string Id, string SessionId, string FilePath, string? Tool, string? Op, System.DateTimeOffset OccurredAt, string? EditSummary);

public sealed class EditsIndex
{
    private readonly string _dbPath;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;

    public EditsIndex(string dataDir, ILogger logger, TimeProvider? timeProvider = null)
    {
        _dbPath = System.IO.Path.Combine(dataDir, "fts", "index.db");
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void RecordEdit(string sessionId, string filePath, string? tool, string? op, string? editSummary)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO agent_edits (id, session_id, file_path, tool, op, occurred_at, edit_summary)
            VALUES (@id, @sid, @fp, @tool, @op, @ts, @summary);
            """;
        cmd.Parameters.AddWithValue("@id", System.Guid.NewGuid().ToString("N"));
        cmd.Parameters.AddWithValue("@sid", sessionId);
        cmd.Parameters.AddWithValue("@fp", filePath);
        cmd.Parameters.AddWithValue("@tool", (object?)tool ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@op", (object?)op ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@ts", _timeProvider.GetUtcNow().ToString("o"));
        cmd.Parameters.AddWithValue("@summary", (object?)editSummary ?? System.DBNull.Value);
        cmd.ExecuteNonQuery();

        _logger.LogWarning("edits: recorded edit to {file} by session {sid}", filePath, sessionId);
    }

    public System.Collections.Generic.IReadOnlyList<EditRecord> FindByFile(string filePath, int limit = 20)
    {
        var result = new System.Collections.Generic.List<EditRecord>();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, session_id, file_path, tool, op, occurred_at, edit_summary FROM agent_edits WHERE file_path = @fp ORDER BY occurred_at DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@fp", filePath);
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(ReadRecord(reader));

        if (result.Count == 0)
        {
            var basename = System.IO.Path.GetFileName(filePath);
            using var bnCmd = conn.CreateCommand();
            bnCmd.CommandText = "SELECT id, session_id, file_path, tool, op, occurred_at, edit_summary FROM agent_edits WHERE file_path LIKE @bn ORDER BY occurred_at DESC LIMIT @limit";
            bnCmd.Parameters.AddWithValue("@bn", "%" + basename);
            bnCmd.Parameters.AddWithValue("@limit", limit);
            using var bnReader = bnCmd.ExecuteReader();
            while (bnReader.Read())
                result.Add(ReadRecord(bnReader));
        }

        return result;
    }

    public System.Collections.Generic.IReadOnlyList<EditRecord> FindBySession(string sessionId, int limit = 50)
    {
        var result = new System.Collections.Generic.List<EditRecord>();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, session_id, file_path, tool, op, occurred_at, edit_summary FROM agent_edits WHERE session_id = @sid ORDER BY occurred_at DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@sid", sessionId);
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(ReadRecord(reader));
        return result;
    }

    private static EditRecord ReadRecord(SqliteDataReader reader)
    {
        return new EditRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            System.DateTimeOffset.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
            reader.IsDBNull(6) ? null : reader.GetString(6)
        );
    }
}
