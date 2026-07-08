using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed record SnapshotEntry(string Id, string WorkspaceId, string Trigger, System.DateTimeOffset CreatedAt, string? Summary);

public sealed class SnapshotInspectService
{
    private readonly string _dbPath;
    private readonly ILogger _logger;

    public SnapshotInspectService(string dataDir, ILogger logger)
    {
        _dbPath = System.IO.Path.Combine(dataDir, "library", "catalog.db");
        _logger = logger;
    }

    public System.Collections.Generic.IReadOnlyList<SnapshotEntry> ListSnapshots(string workspaceId)
    {
        var result = new System.Collections.Generic.List<SnapshotEntry>();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        EnsureSnapshotsTable(conn);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, workspace_id, trigger, created_at, summary FROM snapshots WHERE workspace_id = @ws ORDER BY created_at DESC";
        cmd.Parameters.AddWithValue("@ws", workspaceId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new SnapshotEntry(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                System.DateTimeOffset.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
                reader.IsDBNull(4) ? null : reader.GetString(4)
            ));
        }
        return result;
    }

    public SnapshotEntry CreateSnapshot(string workspaceId, string trigger, string? summary)
    {
        var id = System.Guid.NewGuid().ToString("N");
        var createdAt = System.DateTimeOffset.UtcNow;
        var entry = new SnapshotEntry(id, workspaceId, trigger, createdAt, summary);

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        EnsureSnapshotsTable(conn);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO snapshots (id, workspace_id, trigger, created_at, summary) VALUES (@id, @ws, @trig, @ts, @sum)";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@ws", workspaceId);
        cmd.Parameters.AddWithValue("@trig", trigger);
        cmd.Parameters.AddWithValue("@ts", createdAt.ToString("o"));
        cmd.Parameters.AddWithValue("@sum", (object?)summary ?? System.DBNull.Value);
        cmd.ExecuteNonQuery();

        _logger.LogWarning("snapshots: created {id} ({trigger}) for {ws}", id, trigger, workspaceId);
        return entry;
    }

    public System.Collections.Generic.IReadOnlyList<DiffEntry> InspectDiff(string workspaceId, string snapshotId)
    {
        var result = new System.Collections.Generic.List<DiffEntry>();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        EnsureDiffTable(conn);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT key, old_value, new_value, change_type FROM snapshot_diffs WHERE workspace_id = @ws AND snapshot_id = @sid ORDER BY key";
        cmd.Parameters.AddWithValue("@ws", workspaceId);
        cmd.Parameters.AddWithValue("@sid", snapshotId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new DiffEntry(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3)
            ));
        }
        return result;
    }

    public void RecordDiff(string workspaceId, string snapshotId, string key, string? oldValue, string? newValue, string changeType)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        EnsureDiffTable(conn);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO snapshot_diffs (id, workspace_id, snapshot_id, key, old_value, new_value, change_type) VALUES (@id, @ws, @sid, @key, @old, @new, @ct)";
        cmd.Parameters.AddWithValue("@id", System.Guid.NewGuid().ToString("N"));
        cmd.Parameters.AddWithValue("@ws", workspaceId);
        cmd.Parameters.AddWithValue("@sid", snapshotId);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@old", (object?)oldValue ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@new", (object?)newValue ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@ct", changeType);
        cmd.ExecuteNonQuery();
    }

    public string? Restore(string workspaceId, string snapshotId)
    {
        var preRestore = CreateSnapshot(workspaceId, "pre-restore", $"Auto-snapshot before restoring {snapshotId}");
        _logger.LogWarning("snapshots: created pre-restore snapshot {id} before restoring {snap}", preRestore.Id, snapshotId);
        return preRestore.Id;
    }

    private static void EnsureSnapshotsTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS snapshots (
                id TEXT PRIMARY KEY,
                workspace_id TEXT NOT NULL,
                trigger TEXT NOT NULL,
                created_at TEXT NOT NULL,
                summary TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_snapshots_workspace ON snapshots (workspace_id, created_at DESC);
            """;
        cmd.ExecuteNonQuery();
    }

    private static void EnsureDiffTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS snapshot_diffs (
                id TEXT PRIMARY KEY,
                workspace_id TEXT NOT NULL,
                snapshot_id TEXT NOT NULL,
                key TEXT NOT NULL,
                old_value TEXT,
                new_value TEXT,
                change_type TEXT NOT NULL,
                FOREIGN KEY (snapshot_id) REFERENCES snapshots (id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_diffs_snapshot ON snapshot_diffs (snapshot_id);
            """;
        cmd.ExecuteNonQuery();
    }
}

public sealed record DiffEntry(string Key, string? OldValue, string? NewValue, string ChangeType);
