using Cove.Persistence;
using Dapper;
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

    public CardRepository(SqliteConnectionFactory factory) => _factory = factory;

    public CardRow? GetById(string id)
    {
        using var conn = _factory.Open();
        return conn.QueryFirstOrDefault<CardRow>(
            "SELECT id AS Id, workspace_id AS WorkspaceId, task_number AS TaskNumber, title AS Title, description AS Description, status_id AS StatusId, priority AS Priority, size AS Size, assignee AS Assignee, source AS Source, order_key AS OrderKey, current_primary_run_id AS CurrentPrimaryRunId, launch_config_json AS LaunchConfigJson, agent_ref AS AgentRef, skill_selection_json AS SkillSelectionJson, profile_slug AS ProfileSlug, due_at AS DueAt, attachments_json AS AttachmentsJson, comment_ids_json AS CommentIdsJson, created_at AS CreatedAt, updated_at AS UpdatedAt FROM cards WHERE id = @Id",
            new { Id = id });
    }

    public CardRow? GetByWorkspaceAndNumber(string workspaceId, int taskNumber)
    {
        using var conn = _factory.Open();
        return conn.QueryFirstOrDefault<CardRow>(
            "SELECT id AS Id, workspace_id AS WorkspaceId, task_number AS TaskNumber, title AS Title, description AS Description, status_id AS StatusId, priority AS Priority, size AS Size, assignee AS Assignee, source AS Source, order_key AS OrderKey, current_primary_run_id AS CurrentPrimaryRunId, launch_config_json AS LaunchConfigJson, agent_ref AS AgentRef, skill_selection_json AS SkillSelectionJson, profile_slug AS ProfileSlug, due_at AS DueAt, attachments_json AS AttachmentsJson, comment_ids_json AS CommentIdsJson, created_at AS CreatedAt, updated_at AS UpdatedAt FROM cards WHERE workspace_id = @WorkspaceId AND task_number = @TaskNumber",
            new { WorkspaceId = workspaceId, TaskNumber = taskNumber });
    }

    public IReadOnlyList<CardRow> ListByWorkspace(string workspaceId)
    {
        using var conn = _factory.Open();
        var rows = conn.Query<CardRow>(
            "SELECT id AS Id, workspace_id AS WorkspaceId, task_number AS TaskNumber, title AS Title, description AS Description, status_id AS StatusId, priority AS Priority, size AS Size, assignee AS Assignee, source AS Source, order_key AS OrderKey, current_primary_run_id AS CurrentPrimaryRunId, launch_config_json AS LaunchConfigJson, agent_ref AS AgentRef, skill_selection_json AS SkillSelectionJson, profile_slug AS ProfileSlug, due_at AS DueAt, attachments_json AS AttachmentsJson, comment_ids_json AS CommentIdsJson, created_at AS CreatedAt, updated_at AS UpdatedAt FROM cards WHERE workspace_id = @WorkspaceId ORDER BY status_id, order_key",
            new { WorkspaceId = workspaceId });
        return rows.AsList();
    }

    public IReadOnlyList<CardRow> ListByStatus(string statusId)
    {
        using var conn = _factory.Open();
        var rows = conn.Query<CardRow>(
            "SELECT id AS Id, workspace_id AS WorkspaceId, task_number AS TaskNumber, title AS Title, description AS Description, status_id AS StatusId, priority AS Priority, size AS Size, assignee AS Assignee, source AS Source, order_key AS OrderKey, current_primary_run_id AS CurrentPrimaryRunId, launch_config_json AS LaunchConfigJson, agent_ref AS AgentRef, skill_selection_json AS SkillSelectionJson, profile_slug AS ProfileSlug, due_at AS DueAt, attachments_json AS AttachmentsJson, comment_ids_json AS CommentIdsJson, created_at AS CreatedAt, updated_at AS UpdatedAt FROM cards WHERE status_id = @StatusId ORDER BY order_key",
            new { StatusId = statusId });
        return rows.AsList();
    }

    public void Insert(CardRow row)
    {
        using var conn = _factory.Open();
        conn.Execute(
            "INSERT INTO cards (id, workspace_id, task_number, title, description, status_id, priority, size, assignee, source, order_key, current_primary_run_id, launch_config_json, agent_ref, skill_selection_json, profile_slug, due_at, attachments_json, comment_ids_json, created_at, updated_at) VALUES (@Id, @WorkspaceId, @TaskNumber, @Title, @Description, @StatusId, @Priority, @Size, @Assignee, @Source, @OrderKey, @CurrentPrimaryRunId, @LaunchConfigJson, @AgentRef, @SkillSelectionJson, @ProfileSlug, @DueAt, @AttachmentsJson, @CommentIdsJson, @CreatedAt, @UpdatedAt)",
            row);
    }

    public int Update(CardRow row)
    {
        using var conn = _factory.Open();
        return conn.Execute(
            "UPDATE cards SET title = @Title, description = @Description, status_id = @StatusId, priority = @Priority, size = @Size, assignee = @Assignee, order_key = @OrderKey, current_primary_run_id = @CurrentPrimaryRunId, launch_config_json = @LaunchConfigJson, agent_ref = @AgentRef, skill_selection_json = @SkillSelectionJson, profile_slug = @ProfileSlug, due_at = @DueAt, attachments_json = @AttachmentsJson, comment_ids_json = @CommentIdsJson, updated_at = @UpdatedAt WHERE id = @Id",
            row);
    }

    public int Delete(string id)
    {
        using var conn = _factory.Open();
        return conn.Execute("DELETE FROM cards WHERE id = @Id", new { Id = id });
    }

    public int CountByWorkspace(string workspaceId)
    {
        using var conn = _factory.Open();
        return conn.ExecuteScalar<int>("SELECT count(*) FROM cards WHERE workspace_id = @WorkspaceId", new { WorkspaceId = workspaceId });
    }
}
