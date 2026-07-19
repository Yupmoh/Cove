using Cove.Persistence;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Cove.Tasks.Store;

public sealed class StatusRow
{
    public string BayId { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string HexColor { get; set; } = "808080";
    public double Position { get; set; }
    public bool Hidden { get; set; }
    public bool IsLooping { get; set; }
    public bool IsInProgress { get; set; }
    public bool IsReview { get; set; }
    public bool IsCompletion { get; set; }
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
}

public sealed class StatusRepository
{
    private readonly SqliteConnectionFactory _factory;
    private readonly TasksWriteChannel? _channel;

    private const string SelectColumns = "bay_id AS BayId, id AS Id, name AS Name, hex_color AS HexColor, position AS Position, hidden AS Hidden, is_looping AS IsLooping, is_in_progress AS IsInProgress, is_review AS IsReview, is_completion AS IsCompletion, created_at AS CreatedAt, updated_at AS UpdatedAt";

    public StatusRepository(SqliteConnectionFactory factory, TasksWriteChannel? channel = null)
    {
        _factory = factory;
        _channel = channel;
    }

    public System.Threading.Tasks.Task<StatusRow?> CreateAsync(string bayId, string id, string name, string hexColor, double position)
    {
        if (_channel is null)
            return System.Threading.Tasks.Task.FromResult(CreateSync(bayId, id, name, hexColor, position));
        return _channel.ExecuteAsync(conn => System.Threading.Tasks.Task.FromResult(CreateInternal(conn, bayId, id, name, hexColor, position)));
    }

    private StatusRow? CreateSync(string bayId, string id, string name, string hexColor, double position)
    {
        using var conn = _factory.Open();
        return CreateInternal(conn, bayId, id, name, hexColor, position);
    }

    private static StatusRow? CreateInternal(SqliteConnection conn, string bayId, string id, string name, string hexColor, double position)
    {
        var now = System.DateTimeOffset.UtcNow.ToString("o");
        try
        {
            conn.Execute(
                "INSERT INTO statuses (bay_id, id, name, hex_color, position, hidden, is_looping, is_in_progress, is_review, is_completion, created_at, updated_at) VALUES (@BayId, @Id, @Name, @HexColor, @Position, 0, 0, 0, 0, 0, @Now, @Now)",
                new { BayId = bayId, Id = id, Name = name, HexColor = hexColor, Position = position, Now = now });
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            return null;
        }
        return new StatusRow { BayId = bayId, Id = id, Name = name, HexColor = hexColor, Position = position, CreatedAt = now, UpdatedAt = now };
    }

    public StatusRow? GetByBayAndId(string bayId, string id)
    {
        using var conn = _factory.Open();
        return conn.QueryFirstOrDefault<StatusRow>(
            $"SELECT {SelectColumns} FROM statuses WHERE bay_id = @BayId AND id = @Id",
            new { BayId = bayId, Id = id });
    }

    public IReadOnlyList<StatusRow> ListByBay(string bayId, bool includeHidden = false)
    {
        using var conn = _factory.Open();
        var sql = includeHidden
            ? $"SELECT {SelectColumns} FROM statuses WHERE bay_id = @BayId ORDER BY position"
            : $"SELECT {SelectColumns} FROM statuses WHERE bay_id = @BayId AND hidden = 0 ORDER BY position";
        return conn.Query<StatusRow>(sql, new { BayId = bayId }).AsList();
    }

    public System.Threading.Tasks.Task SetHiddenAsync(string bayId, string id, bool hidden)
    {
        if (_channel is null)
        {
            SetHiddenSync(bayId, id, hidden);
            return System.Threading.Tasks.Task.CompletedTask;
        }
        return _channel.ExecuteAsync(conn => { SetHiddenInternal(conn, bayId, id, hidden); return System.Threading.Tasks.Task.CompletedTask; });
    }

    private void SetHiddenSync(string bayId, string id, bool hidden)
    {
        using var conn = _factory.Open();
        SetHiddenInternal(conn, bayId, id, hidden);
    }

    private static void SetHiddenInternal(SqliteConnection conn, string bayId, string id, bool hidden)
    {
        conn.Execute(
            "UPDATE statuses SET hidden = @Hidden, updated_at = @Now WHERE bay_id = @BayId AND id = @Id",
            new { Hidden = hidden ? 1 : 0, Now = System.DateTimeOffset.UtcNow.ToString("o"), BayId = bayId, Id = id });
    }

    public System.Threading.Tasks.Task ReorderAsync(string bayId, string[] orderedIds)
    {
        if (_channel is null)
        {
            ReorderSync(bayId, orderedIds);
            return System.Threading.Tasks.Task.CompletedTask;
        }
        return _channel.ExecuteAsync(conn => { ReorderInternal(conn, bayId, orderedIds); return System.Threading.Tasks.Task.CompletedTask; });
    }

    private void ReorderSync(string bayId, string[] orderedIds)
    {
        using var conn = _factory.Open();
        ReorderInternal(conn, bayId, orderedIds);
    }

    private static void ReorderInternal(SqliteConnection conn, string bayId, string[] orderedIds)
    {
        var now = System.DateTimeOffset.UtcNow.ToString("o");
        for (int i = 0; i < orderedIds.Length; i++)
        {
            conn.Execute(
                "UPDATE statuses SET position = @Position, updated_at = @Now WHERE bay_id = @BayId AND id = @Id",
                new { Position = (double)i, Now = now, BayId = bayId, Id = orderedIds[i] });
        }
    }

    public async System.Threading.Tasks.Task DeleteAsync(string bayId, string id, string? rehomeToStatusId)
    {
        if (_channel is null)
        {
            DeleteSync(bayId, id, rehomeToStatusId);
            return;
        }
        await _channel.ExecuteTransactionAsync<object?>((conn, transaction) =>
        {
            DeleteInternal(conn, bayId, id, rehomeToStatusId, transaction);
            return System.Threading.Tasks.Task.FromResult<object?>(null);
        });
    }

    private void DeleteSync(string bayId, string id, string? rehomeToStatusId)
    {
        using var conn = _factory.Open();
        DeleteInternal(conn, bayId, id, rehomeToStatusId);
    }

    private static void DeleteInternal(
        SqliteConnection conn,
        string bayId,
        string id,
        string? rehomeToStatusId,
        SqliteTransaction? transaction = null)
    {
        var cardCount = conn.ExecuteScalar<int>(
            "SELECT count(*) FROM cards WHERE bay_id = @BayId AND status_id = @Id",
            new { BayId = bayId, Id = id },
            transaction);
        if (cardCount > 0)
        {
            if (rehomeToStatusId is null)
                throw new System.InvalidOperationException($"cannot delete status '{id}' with {cardCount} cards without rehome target");
            conn.Execute(
                "UPDATE cards SET status_id = @RehomeTo, updated_at = @Now WHERE bay_id = @BayId AND status_id = @Id",
                new { RehomeTo = rehomeToStatusId, Now = System.DateTimeOffset.UtcNow.ToString("o"), BayId = bayId, Id = id },
                transaction);
        }
        conn.Execute(
            "DELETE FROM statuses WHERE bay_id = @BayId AND id = @Id",
            new { BayId = bayId, Id = id },
            transaction);
    }
}
