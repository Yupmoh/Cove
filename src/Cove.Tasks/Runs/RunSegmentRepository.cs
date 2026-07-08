using Cove.Persistence;
using Cove.Tasks.Store;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Cove.Tasks.Runs;

public sealed class RunSegmentRow
{
    public string Id { get; set; } = "";
    public string RunId { get; set; } = "";
    public string? PaneId { get; set; }
    public string? AdapterSessionId { get; set; }
    public string StartedAt { get; set; } = "";
    public string? EndedAt { get; set; }
    public string CreatedAt { get; set; } = "";
}

public sealed class RunSegmentRepository
{
    private readonly SqliteConnectionFactory _factory;
    private readonly TasksWriteChannel? _channel;

    private const string SelectColumns = "id AS Id, run_id AS RunId, pane_id AS PaneId, adapter_session_id AS AdapterSessionId, started_at AS StartedAt, ended_at AS EndedAt, created_at AS CreatedAt";

    public RunSegmentRepository(SqliteConnectionFactory factory, TasksWriteChannel? channel = null)
    {
        _factory = factory;
        _channel = channel;
    }

    public System.Threading.Tasks.Task<RunSegmentRow?> AddAsync(string runId, string? paneId, string? adapterSessionId)
    {
        var row = new RunSegmentRow
        {
            Id = System.Guid.NewGuid().ToString("N"),
            RunId = runId,
            PaneId = paneId,
            AdapterSessionId = adapterSessionId,
            StartedAt = System.DateTimeOffset.UtcNow.ToString("o"),
            CreatedAt = System.DateTimeOffset.UtcNow.ToString("o"),
        };
        if (_channel is null)
        {
            AddSync(row);
            return System.Threading.Tasks.Task.FromResult<RunSegmentRow?>(row);
        }
        return _channel.ExecuteAsync<RunSegmentRow?>(conn => { AddInternal(conn, row); return System.Threading.Tasks.Task.FromResult<RunSegmentRow?>(row); });
    }

    private void AddSync(RunSegmentRow row)
    {
        using var conn = _factory.Open();
        AddInternal(conn, row);
    }

    private static void AddInternal(SqliteConnection conn, RunSegmentRow row)
    {
        conn.Execute(
            "INSERT INTO task_run_segments (id, run_id, pane_id, adapter_session_id, started_at, ended_at, created_at) VALUES (@Id, @RunId, @PaneId, @AdapterSessionId, @StartedAt, @EndedAt, @CreatedAt)",
            row);
    }

    public IReadOnlyList<RunSegmentRow> ListByRun(string runId)
    {
        using var conn = _factory.Open();
        return conn.Query<RunSegmentRow>(
            $"SELECT {SelectColumns} FROM task_run_segments WHERE run_id = @RunId ORDER BY started_at",
            new { RunId = runId }).AsList();
    }

    public System.Threading.Tasks.Task EndAsync(string segmentId)
    {
        if (_channel is null)
        {
            EndSync(segmentId);
            return System.Threading.Tasks.Task.CompletedTask;
        }
        return _channel.ExecuteAsync(conn => { EndInternal(conn, segmentId); return System.Threading.Tasks.Task.CompletedTask; });
    }

    private void EndSync(string segmentId)
    {
        using var conn = _factory.Open();
        EndInternal(conn, segmentId);
    }

    private static void EndInternal(SqliteConnection conn, string segmentId)
    {
        conn.Execute(
            "UPDATE task_run_segments SET ended_at = @Now WHERE id = @Id",
            new { Now = System.DateTimeOffset.UtcNow.ToString("o"), Id = segmentId });
    }
}
