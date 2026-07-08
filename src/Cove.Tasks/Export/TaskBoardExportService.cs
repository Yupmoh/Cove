using Cove.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Tasks.Export;

public sealed record ExportManifest(System.DateTimeOffset ExportedAt, int SchemaVersion, int WorkspaceCount);
public sealed record ExportResult(bool Success, string? ExportPath, ExportManifest? Manifest, string? Error);
public sealed record RestoreDiffResult(bool Success, System.Collections.Generic.IReadOnlyList<RowDiff> Diffs, string? Error);

public sealed record RowDiff(string Table, string Id, string ChangeType, string? Before, string? After);

public interface ISnapshotSink
{
    System.Threading.Tasks.Task<bool> WriteSnapshotAsync(string workspaceId, byte[] data, ExportManifest manifest);
}

public sealed class NullSnapshotSink : ISnapshotSink
{
    public System.Threading.Tasks.Task<bool> WriteSnapshotAsync(string workspaceId, byte[] data, ExportManifest manifest)
        => System.Threading.Tasks.Task.FromResult(true);
}

public sealed class TaskBoardExportService
{
    private readonly SqliteConnectionFactory _factory;
    private readonly ILogger _logger;

    public TaskBoardExportService(SqliteConnectionFactory factory, ILogger logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public ExportResult Export(string exportPath, int workspaceCount)
    {
        try
        {
            using var conn = _factory.Open();
            using (var checkpointCmd = conn.CreateCommand())
            {
                checkpointCmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
                checkpointCmd.ExecuteNonQuery();
            }
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"VACUUM INTO '{exportPath.Replace("'", "''")}'";
            cmd.ExecuteNonQuery();
            var manifest = new ExportManifest(System.DateTimeOffset.UtcNow, 1, workspaceCount);
            _logger.LogWarning("export: tasks.db exported to {path} ({workspaceCount} workspaces)", exportPath, workspaceCount);
            return new ExportResult(true, exportPath, manifest, null);
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning("export: failed to export tasks.db: {error}", ex.Message);
            return new ExportResult(false, null, null, ex.Message);
        }
    }

    public RestoreDiffResult DiffAgainst(string importPath)
    {
        try
        {
            var diffs = new System.Collections.Generic.List<RowDiff>();
            using var conn = _factory.Open();

            foreach (var (table, keyCol) in new (string Table, string KeyCol)[] { ("cards", "id"), ("statuses", "id"), ("labels", "id"), ("comments", "id"), ("task_runs", "id"), ("task_run_segments", "id"), ("card_schedules", "card_id") })
            {
                diffs.AddRange(DiffTable(conn, importPath, table, keyCol));
            }

            return new RestoreDiffResult(true, diffs, null);
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning("restore-diff: failed to diff against {path}: {error}", importPath, ex.Message);
            return new RestoreDiffResult(false, [], ex.Message);
        }
    }

    private static System.Collections.Generic.IReadOnlyList<RowDiff> DiffTable(SqliteConnection conn, string importPath, string table, string keyCol)
    {
        var diffs = new System.Collections.Generic.List<RowDiff>();

        var currentIds = new System.Collections.Generic.HashSet<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT {keyCol} FROM {table}";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                currentIds.Add(reader.GetString(0));
        }

        using var importConn = new SqliteConnection($"Data Source={importPath};Mode=ReadOnly");
        importConn.Open();
        var importIds = new System.Collections.Generic.HashSet<string>();
        using (var cmd = importConn.CreateCommand())
        {
            cmd.CommandText = $"SELECT {keyCol} FROM {table}";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                importIds.Add(reader.GetString(0));
        }

        foreach (var id in importIds)
        {
            if (!currentIds.Contains(id))
                diffs.Add(new RowDiff(table, id, "added", null, null));
        }

        foreach (var id in currentIds)
        {
            if (!importIds.Contains(id))
                diffs.Add(new RowDiff(table, id, "removed", null, null));
        }

        return diffs;
    }
}
