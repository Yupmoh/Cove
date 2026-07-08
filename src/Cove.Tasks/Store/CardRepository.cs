using Cove.Persistence;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Cove.Tasks.Store;

public sealed class CardRow
{
    public string Id { get; set; } = "";
    public string WorkspaceId { get; set; } = "";
    public int TaskNumber { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string StatusId { get; set; } = "todo";
    public int Priority { get; set; } = 1;
    public int Size { get; set; } = 2;
    public string? Assignee { get; set; }
    public string Source { get; set; } = "";
    public double OrderKey { get; set; }
    public string? CurrentPrimaryRunId { get; set; }
    public string? LaunchConfigJson { get; set; }
    public string? AgentRef { get; set; }
    public string? SkillSelectionJson { get; set; }
    public string? ProfileSlug { get; set; }
    public string? DueAt { get; set; }
    public string? AttachmentsJson { get; set; }
    public string CommentIdsJson { get; set; } = "[]";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
}

public sealed class CardRepository
{
    private readonly SqliteConnectionFactory _factory;
    private readonly TasksWriteChannel? _channel;

    private const string SelectColumns = "id AS Id, workspace_id AS WorkspaceId, task_number AS TaskNumber, title AS Title, description AS Description, status_id AS StatusId, priority AS Priority, size AS Size, assignee AS Assignee, source AS Source, order_key AS OrderKey, current_primary_run_id AS CurrentPrimaryRunId, launch_config_json AS LaunchConfigJson, agent_ref AS AgentRef, skill_selection_json AS SkillSelectionJson, profile_slug AS ProfileSlug, due_at AS DueAt, attachments_json AS AttachmentsJson, comment_ids_json AS CommentIdsJson, created_at AS CreatedAt, updated_at AS UpdatedAt";

    public CardRepository(SqliteConnectionFactory factory, TasksWriteChannel? channel = null)
    {
        _factory = factory;
        _channel = channel;
    }

    public CardRow? GetById(string id)
    {
        using var conn = _factory.Open();
        return conn.QueryFirstOrDefault<CardRow>(
            $"SELECT {SelectColumns} FROM cards WHERE id = @Id",
            new { Id = id });
    }

    public CardRow? GetByWorkspaceAndNumber(string workspaceId, int taskNumber)
    {
        using var conn = _factory.Open();
        return conn.QueryFirstOrDefault<CardRow>(
            $"SELECT {SelectColumns} FROM cards WHERE workspace_id = @WorkspaceId AND task_number = @TaskNumber",
            new { WorkspaceId = workspaceId, TaskNumber = taskNumber });
    }

    public IReadOnlyList<CardRow> ListByWorkspace(string workspaceId)
    {
        using var conn = _factory.Open();
        return conn.Query<CardRow>(
            $"SELECT {SelectColumns} FROM cards WHERE workspace_id = @WorkspaceId ORDER BY status_id, order_key",
            new { WorkspaceId = workspaceId }).AsList();
    }

    public IReadOnlyList<CardRow> ListByStatus(string workspaceId, string statusId)
    {
        using var conn = _factory.Open();
        return conn.Query<CardRow>(
            $"SELECT {SelectColumns} FROM cards WHERE workspace_id = @WorkspaceId AND status_id = @StatusId ORDER BY order_key",
            new { WorkspaceId = workspaceId, StatusId = statusId }).AsList();
    }

    public System.Threading.Tasks.Task InsertAsync(CardRow row)
    {
        if (_channel is null)
        {
            InsertSync(row);
            return System.Threading.Tasks.Task.CompletedTask;
        }
        return _channel.ExecuteAsync(conn => { InsertInternal(conn, row); return System.Threading.Tasks.Task.CompletedTask; });
    }

    private void InsertSync(CardRow row)
    {
        using var conn = _factory.Open();
        InsertInternal(conn, row);
    }

    private static void InsertInternal(SqliteConnection conn, CardRow row)
    {
        conn.Execute(
            "INSERT INTO cards (id, workspace_id, task_number, title, description, status_id, priority, size, assignee, source, order_key, current_primary_run_id, launch_config_json, agent_ref, skill_selection_json, profile_slug, due_at, attachments_json, comment_ids_json, created_at, updated_at) VALUES (@Id, @WorkspaceId, @TaskNumber, @Title, @Description, @StatusId, @Priority, @Size, @Assignee, @Source, @OrderKey, @CurrentPrimaryRunId, @LaunchConfigJson, @AgentRef, @SkillSelectionJson, @ProfileSlug, @DueAt, @AttachmentsJson, @CommentIdsJson, @CreatedAt, @UpdatedAt)",
            row);
    }

    public System.Threading.Tasks.Task<int> UpdateAsync(CardRow row)
    {
        if (_channel is null)
            return System.Threading.Tasks.Task.FromResult(UpdateSync(row));
        return _channel.ExecuteAsync(conn => System.Threading.Tasks.Task.FromResult(UpdateInternal(conn, row)));
    }

    private int UpdateSync(CardRow row)
    {
        using var conn = _factory.Open();
        return UpdateInternal(conn, row);
    }

    private static int UpdateInternal(SqliteConnection conn, CardRow row)
    {
        return conn.Execute(
            "UPDATE cards SET title = @Title, description = @Description, status_id = @StatusId, priority = @Priority, size = @Size, assignee = @Assignee, order_key = @OrderKey, current_primary_run_id = @CurrentPrimaryRunId, launch_config_json = @LaunchConfigJson, agent_ref = @AgentRef, skill_selection_json = @SkillSelectionJson, profile_slug = @ProfileSlug, due_at = @DueAt, attachments_json = @AttachmentsJson, comment_ids_json = @CommentIdsJson, updated_at = @UpdatedAt WHERE id = @Id",
            row);
    }

    public System.Threading.Tasks.Task<int> DeleteAsync(string id)
    {
        if (_channel is null)
            return System.Threading.Tasks.Task.FromResult(DeleteSync(id));
        return _channel.ExecuteAsync(conn => System.Threading.Tasks.Task.FromResult(DeleteInternal(conn, id)));
    }

    private int DeleteSync(string id)
    {
        using var conn = _factory.Open();
        return DeleteInternal(conn, id);
    }

    private static int DeleteInternal(SqliteConnection conn, string id)
        => conn.Execute("DELETE FROM cards WHERE id = @Id", new { Id = id });

    public int CountByWorkspace(string workspaceId)
    {
        using var conn = _factory.Open();
        return conn.ExecuteScalar<int>("SELECT count(*) FROM cards WHERE workspace_id = @WorkspaceId", new { WorkspaceId = workspaceId });
    }

    public System.Threading.Tasks.Task<double> NextOrderKeyAsync(string workspaceId, string statusId)
    {
        if (_channel is null)
            return System.Threading.Tasks.Task.FromResult(NextOrderKeySync(workspaceId, statusId));
        return _channel.ExecuteAsync(conn => System.Threading.Tasks.Task.FromResult(NextOrderKeyInternal(conn, workspaceId, statusId)));
    }

    private double NextOrderKeySync(string workspaceId, string statusId)
    {
        using var conn = _factory.Open();
        return NextOrderKeyInternal(conn, workspaceId, statusId);
    }

    private static double NextOrderKeyInternal(SqliteConnection conn, string workspaceId, string statusId)
    {
        var min = conn.ExecuteScalar<double?>("SELECT MIN(order_key) FROM cards WHERE workspace_id = @WorkspaceId AND status_id = @StatusId", new { WorkspaceId = workspaceId, StatusId = statusId });
        return min.HasValue ? min.Value - 1.0 : 0.0;
    }

    public System.Threading.Tasks.Task MoveToPositionAsync(string workspaceId, string statusId, string cardId, string? beforeId)
    {
        if (_channel is null)
        {
            MoveToPositionSync(workspaceId, statusId, cardId, beforeId);
            return System.Threading.Tasks.Task.CompletedTask;
        }
        return _channel.ExecuteAsync(conn => { MoveToPositionInternal(conn, workspaceId, statusId, cardId, beforeId); return System.Threading.Tasks.Task.CompletedTask; });
    }

    private void MoveToPositionSync(string workspaceId, string statusId, string cardId, string? beforeId)
    {
        using var conn = _factory.Open();
        MoveToPositionInternal(conn, workspaceId, statusId, cardId, beforeId);
    }

    private static void MoveToPositionInternal(SqliteConnection conn, string workspaceId, string statusId, string cardId, string? beforeId)
    {
        var rows = conn.Query<CardOrderRow>(
            "SELECT id AS Id, order_key AS OrderKey FROM cards WHERE workspace_id = @WorkspaceId AND status_id = @StatusId ORDER BY order_key",
            new { WorkspaceId = workspaceId, StatusId = statusId }).AsList();

        var cardIdx = rows.FindIndex(r => r.Id == cardId);
        if (cardIdx < 0) return;
        var card = rows[cardIdx];
        rows.RemoveAt(cardIdx);

        int insertIdx;
        double newKey;
        if (beforeId is null)
        {
            insertIdx = rows.Count;
            newKey = rows.Count == 0 ? 0.0 : rows[^1].OrderKey + 1.0;
        }
        else
        {
            insertIdx = rows.FindIndex(r => r.Id == beforeId);
            if (insertIdx < 0) insertIdx = rows.Count;
            if (insertIdx == 0)
                newKey = rows.Count == 0 ? 0.0 : rows[0].OrderKey - 1.0;
            else
                newKey = (rows[insertIdx - 1].OrderKey + rows[insertIdx].OrderKey) / 2.0;
        }

        conn.Execute("UPDATE cards SET order_key = @OrderKey, updated_at = @UpdatedAt WHERE id = @Id",
            new { OrderKey = newKey, UpdatedAt = System.DateTimeOffset.UtcNow.ToString("o"), Id = card.Id });
    }
}

internal sealed class CardOrderRow
{
    public string Id { get; set; } = "";
    public double OrderKey { get; set; }
}
