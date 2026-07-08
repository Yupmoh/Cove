using Cove.Persistence;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Cove.Tasks.Store;

public sealed class TaskCounterRepository
{
    private readonly SqliteConnectionFactory _factory;
    private readonly TasksWriteChannel _channel;

    public TaskCounterRepository(SqliteConnectionFactory factory, TasksWriteChannel channel)
    {
        _factory = factory;
        _channel = channel;
    }

    public async System.Threading.Tasks.Task<int> NextNumberAsync(string workspaceId)
    {
        return await _channel.ExecuteAsync(conn => System.Threading.Tasks.Task.FromResult(AllocateNumber(conn, workspaceId)));
    }

    private static int AllocateNumber(SqliteConnection conn, string workspaceId)
    {
        return conn.ExecuteScalar<int>(
            """
            INSERT INTO task_counter (workspace_id, next_number)
            VALUES (@WorkspaceId, 2)
            ON CONFLICT(workspace_id) DO UPDATE SET next_number = next_number + 1
            RETURNING next_number - 1
            """,
            new { WorkspaceId = workspaceId });
    }

    public int PeekNumber(string workspaceId)
    {
        using var conn = _factory.Open();
        var v = conn.ExecuteScalar<int>(
            "SELECT next_number FROM task_counter WHERE workspace_id = @WorkspaceId",
            new { WorkspaceId = workspaceId });
        return v == 0 ? 1 : v;
    }
}
