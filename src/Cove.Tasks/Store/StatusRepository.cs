using Cove.Persistence;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Cove.Tasks.Store;

public sealed class StatusRow
{
    public string WorkspaceId { get; set; } = "";
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

    private const string SelectColumns = "workspace_id AS WorkspaceId, id AS Id, name AS Name, hex_color AS HexColor, position AS Position, hidden AS Hidden, is_looping AS IsLooping, is_in_progress AS IsInProgress, is_review AS IsReview, is_completion AS IsCompletion, created_at AS CreatedAt, updated_at AS UpdatedAt";

    public StatusRepository(SqliteConnectionFactory factory, TasksWriteChannel? channel = null)
    {
        _factory = factory;
        _channel = channel;
    }

    public System.Threading.Tasks.Task<StatusRow?> CreateAsync(string workspaceId, string id, string name, string hexColor, double position)
    {
        if (_channel is null)
            return System.Threading.Tasks.Task.FromResult(CreateSync(workspaceId, id, name, hexColor, position));
        return _channel.ExecuteAsync(conn => System.Threading.Tasks.Task.FromResult(CreateInternal(conn, workspaceId, id, name, hexColor, position)));
    }

    private StatusRow? CreateSync(string workspaceId, string id, string name, string hexColor, double position)
    {
        using var conn = _factory.Open();
        return CreateInternal(conn, workspaceId, id, name, hexColor, position);
    }

    private static StatusRow? CreateInternal(SqliteConnection conn, string workspaceId, string id, string name, string hexColor, double position)
    {
        var now = System.DateTimeOffset.UtcNow.ToString("o");
        try
        {
            conn.Execute(
                "INSERT INTO statuses (workspace_id, id, name, hex_color, position, hidden, is_looping, is_in_progress, is_review, is_completion, created_at, updated_at) VALUES (@WorkspaceId, @Id, @Name, @HexColor, @Position, 0, 0, 0, 0, 0, @Now, @Now)",
                new { WorkspaceId = workspaceId, Id = id, Name = name, HexColor = hexColor, Position = position, Now = now });
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            return null;
        }
        return new StatusRow { WorkspaceId = workspaceId, Id = id, Name = name, HexColor = hexColor, Position = position, CreatedAt = now, UpdatedAt = now };
    }

    public StatusRow? GetByWorkspaceAndId(string workspaceId, string id)
    {
        using var conn = _factory.Open();
        return conn.QueryFirstOrDefault<StatusRow>(
            $"SELECT {SelectColumns} FROM statuses WHERE workspace_id = @WorkspaceId AND id = @Id",
            new { WorkspaceId = workspaceId, Id = id });
    }

    public IReadOnlyList<StatusRow> ListByWorkspace(string workspaceId, bool includeHidden = false)
    {
        using var conn = _factory.Open();
        var sql = includeHidden
            ? $"SELECT {SelectColumns} FROM statuses WHERE workspace_id = @WorkspaceId ORDER BY position"
            : $"SELECT {SelectColumns} FROM statuses WHERE workspace_id = @WorkspaceId AND hidden = 0 ORDER BY position";
        return conn.Query<StatusRow>(sql, new { WorkspaceId = workspaceId }).AsList();
    }

    public System.Threading.Tasks.Task SetHiddenAsync(string workspaceId, string id, bool hidden)
    {
        if (_channel is null)
        {
            SetHiddenSync(workspaceId, id, hidden);
            return System.Threading.Tasks.Task.CompletedTask;
        }
        return _channel.ExecuteAsync(conn => { SetHiddenInternal(conn, workspaceId, id, hidden); return System.Threading.Tasks.Task.CompletedTask; });
    }

    private void SetHiddenSync(string workspaceId, string id, bool hidden)
    {
        using var conn = _factory.Open();
        SetHiddenInternal(conn, workspaceId, id, hidden);
    }

    private static void SetHiddenInternal(SqliteConnection conn, string workspaceId, string id, bool hidden)
    {
        conn.Execute(
            "UPDATE statuses SET hidden = @Hidden, updated_at = @Now WHERE workspace_id = @WorkspaceId AND id = @Id",
            new { Hidden = hidden ? 1 : 0, Now = System.DateTimeOffset.UtcNow.ToString("o"), WorkspaceId = workspaceId, Id = id });
    }

    public System.Threading.Tasks.Task ReorderAsync(string workspaceId, string[] orderedIds)
    {
        if (_channel is null)
        {
            ReorderSync(workspaceId, orderedIds);
            return System.Threading.Tasks.Task.CompletedTask;
        }
        return _channel.ExecuteAsync(conn => { ReorderInternal(conn, workspaceId, orderedIds); return System.Threading.Tasks.Task.CompletedTask; });
    }

    private void ReorderSync(string workspaceId, string[] orderedIds)
    {
        using var conn = _factory.Open();
        ReorderInternal(conn, workspaceId, orderedIds);
    }

    private static void ReorderInternal(SqliteConnection conn, string workspaceId, string[] orderedIds)
    {
        var now = System.DateTimeOffset.UtcNow.ToString("o");
        for (int i = 0; i < orderedIds.Length; i++)
        {
            conn.Execute(
                "UPDATE statuses SET position = @Position, updated_at = @Now WHERE workspace_id = @WorkspaceId AND id = @Id",
                new { Position = (double)i, Now = now, WorkspaceId = workspaceId, Id = orderedIds[i] });
        }
    }

    public async System.Threading.Tasks.Task DeleteAsync(string workspaceId, string id, string? rehomeToStatusId)
    {
        if (_channel is null)
        {
            DeleteSync(workspaceId, id, rehomeToStatusId);
            return;
        }
        await _channel.ExecuteAsync(conn => { DeleteInternal(conn, workspaceId, id, rehomeToStatusId); return System.Threading.Tasks.Task.CompletedTask; });
    }

    private void DeleteSync(string workspaceId, string id, string? rehomeToStatusId)
    {
        using var conn = _factory.Open();
        DeleteInternal(conn, workspaceId, id, rehomeToStatusId);
    }

    private static void DeleteInternal(SqliteConnection conn, string workspaceId, string id, string? rehomeToStatusId)
    {
        var cardCount = conn.ExecuteScalar<int>(
            "SELECT count(*) FROM cards WHERE workspace_id = @WorkspaceId AND status_id = @Id",
            new { WorkspaceId = workspaceId, Id = id });
        if (cardCount > 0)
        {
            if (rehomeToStatusId is null)
                throw new System.InvalidOperationException($"cannot delete status '{id}' with {cardCount} cards without rehome target");
            conn.Execute(
                "UPDATE cards SET status_id = @RehomeTo, updated_at = @Now WHERE workspace_id = @WorkspaceId AND status_id = @Id",
                new { RehomeTo = rehomeToStatusId, Now = System.DateTimeOffset.UtcNow.ToString("o"), WorkspaceId = workspaceId, Id = id });
        }
        conn.Execute(
            "DELETE FROM statuses WHERE workspace_id = @WorkspaceId AND id = @Id",
            new { WorkspaceId = workspaceId, Id = id });
    }
}
