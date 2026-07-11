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

    public async System.Threading.Tasks.Task<int> NextNumberAsync(string bayId)
    {
        return await _channel.ExecuteAsync(conn => System.Threading.Tasks.Task.FromResult(AllocateNumber(conn, bayId)));
    }

    private static int AllocateNumber(SqliteConnection conn, string bayId)
    {
        return conn.ExecuteScalar<int>(
            """
            INSERT INTO task_counter (bay_id, next_number)
            VALUES (@BayId, 2)
            ON CONFLICT(bay_id) DO UPDATE SET next_number = next_number + 1
            RETURNING next_number - 1
            """,
            new { BayId = bayId });
    }

    public int PeekNumber(string bayId)
    {
        using var conn = _factory.Open();
        var v = conn.ExecuteScalar<int>(
            "SELECT next_number FROM task_counter WHERE bay_id = @BayId",
            new { BayId = bayId });
        return v == 0 ? 1 : v;
    }
}
