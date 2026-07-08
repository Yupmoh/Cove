using Cove.Persistence;
using Cove.Tasks.Store;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Cove.Tasks.Runs;

public sealed class RunRow
{
    public string Id { get; set; } = "";
    public string CardId { get; set; } = "";
    public string WorkspaceId { get; set; } = "";
    public string RunFamilyId { get; set; } = "";
    public string State { get; set; } = "active";
    public bool Backgrounded { get; set; }
    public string? LaunchProfileJson { get; set; }
    public string? ReviewStatusId { get; set; }
    public string? CompletionStatusId { get; set; }
    public string StartedAt { get; set; } = "";
    public string? EndedAt { get; set; }
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
}

public sealed class RunRepository
{
    private readonly SqliteConnectionFactory _factory;
    private readonly TasksWriteChannel? _channel;

    private const string SelectColumns = "id AS Id, card_id AS CardId, workspace_id AS WorkspaceId, run_family_id AS RunFamilyId, state AS State, backgrounded AS Backgrounded, launch_profile_json AS LaunchProfileJson, review_status_id AS ReviewStatusId, completion_status_id AS CompletionStatusId, started_at AS StartedAt, ended_at AS EndedAt, created_at AS CreatedAt, updated_at AS UpdatedAt";

    public RunRepository(SqliteConnectionFactory factory, TasksWriteChannel? channel = null)
    {
        _factory = factory;
        _channel = channel;
    }

    public System.Threading.Tasks.Task<RunRow?> CreateAsync(string cardId, string workspaceId, string? launchProfileJson, string? runFamilyId = null, bool backgrounded = false, string? reviewStatusId = null, string? completionStatusId = null)
    {
        var id = System.Guid.NewGuid().ToString("N");
        var row = new RunRow
        {
            Id = id,
            CardId = cardId,
            RunFamilyId = runFamilyId ?? id,
            WorkspaceId = workspaceId,
            State = "active",
            Backgrounded = backgrounded,
            LaunchProfileJson = launchProfileJson,
            ReviewStatusId = reviewStatusId,
            CompletionStatusId = completionStatusId,
            StartedAt = System.DateTimeOffset.UtcNow.ToString("o"),
            CreatedAt = System.DateTimeOffset.UtcNow.ToString("o"),
        };
        if (_channel is null)
        {
            CreateSync(row);
            return System.Threading.Tasks.Task.FromResult<RunRow?>(row);
        }
        return _channel.ExecuteAsync<RunRow?>(conn => { CreateInternal(conn, row); return System.Threading.Tasks.Task.FromResult<RunRow?>(row); });
    }

    private void CreateSync(RunRow row)
    {
        using var conn = _factory.Open();
        CreateInternal(conn, row);
    }

    private static void CreateInternal(SqliteConnection conn, RunRow row)
    {
        conn.Execute(
            "INSERT INTO task_runs (id, card_id, workspace_id, run_family_id, state, backgrounded, launch_profile_json, review_status_id, completion_status_id, started_at, ended_at, created_at, updated_at) VALUES (@Id, @CardId, @WorkspaceId, @RunFamilyId, @State, @Backgrounded, @LaunchProfileJson, @ReviewStatusId, @CompletionStatusId, @StartedAt, @EndedAt, @CreatedAt, @UpdatedAt)",
            row);
    }

    public RunRow? GetById(string id)
    {
        using var conn = _factory.Open();
        return conn.QueryFirstOrDefault<RunRow>($"SELECT {SelectColumns} FROM task_runs WHERE id = @Id", new { Id = id });
    }

    public RunRow? FindByPrefix(string prefix)
    {
        if (prefix.Length < 4) return null;
        using var conn = _factory.Open();
        var matches = conn.Query<RunRow>($"SELECT {SelectColumns} FROM task_runs WHERE id LIKE @Prefix || '%'", new { Prefix = prefix }).AsList();
        return matches.Count == 1 ? matches[0] : null;
    }

    public IReadOnlyList<RunRow> ListByCard(string cardId)
    {
        using var conn = _factory.Open();
        return conn.Query<RunRow>($"SELECT {SelectColumns} FROM task_runs WHERE card_id = @CardId ORDER BY created_at", new { CardId = cardId }).AsList();
    }

    public IReadOnlyList<RunRow> ListByFamily(string runFamilyId)
    {
        using var conn = _factory.Open();
        return conn.Query<RunRow>($"SELECT {SelectColumns} FROM task_runs WHERE run_family_id = @RunFamilyId ORDER BY created_at", new { RunFamilyId = runFamilyId }).AsList();
    }

    public IReadOnlyList<RunRow> ListByState(string state)
    {
        using var conn = _factory.Open();
        return conn.Query<RunRow>($"SELECT {SelectColumns} FROM task_runs WHERE state = @State ORDER BY created_at", new { State = state }).AsList();
    }

    public IReadOnlyList<RunRow> ListByWorkspace(string workspaceId)
    {
        using var conn = _factory.Open();
        return conn.Query<RunRow>($"SELECT {SelectColumns} FROM task_runs WHERE workspace_id = @WorkspaceId ORDER BY created_at", new { WorkspaceId = workspaceId }).AsList();
    }

    public IReadOnlyList<RunRow> ListByCardAndState(string cardId, string state)
    {
        using var conn = _factory.Open();
        return conn.Query<RunRow>($"SELECT {SelectColumns} FROM task_runs WHERE card_id = @CardId AND state = @State ORDER BY created_at", new { CardId = cardId, State = state }).AsList();
    }

    public IReadOnlyList<RunRow> ListByWorkspaceAndState(string workspaceId, string state)
    {
        using var conn = _factory.Open();
        return conn.Query<RunRow>($"SELECT {SelectColumns} FROM task_runs WHERE workspace_id = @WorkspaceId AND state = @State ORDER BY created_at", new { WorkspaceId = workspaceId, State = state }).AsList();
    }

    public bool HasActiveRun(string cardId)
    {
        using var conn = _factory.Open();
        var count = conn.ExecuteScalar<int>(
            "SELECT count(*) FROM task_runs WHERE card_id = @CardId AND state IN ('active', 'interrupted', 'resuming')",
            new { CardId = cardId });
        return count > 0;
    }

    public System.Threading.Tasks.Task TransitionAsync(string runId, RunState newState)
    {
        var run = GetById(runId);
        if (run is null)
            throw new System.InvalidOperationException($"run {runId} not found");
        if (!System.Enum.TryParse<RunState>(run.State, true, out var currentState))
            throw new System.InvalidOperationException($"run {runId} has unknown state {run.State}");
        if (!RunStateTransitions.IsValid(currentState, newState))
            throw new System.InvalidOperationException($"invalid transition {currentState} -> {newState}");

        if (_channel is null)
        {
            TransitionSync(runId, newState);
            return System.Threading.Tasks.Task.CompletedTask;
        }
        return _channel.ExecuteAsync(conn => { TransitionInternal(conn, runId, newState); return System.Threading.Tasks.Task.CompletedTask; });
    }

    private void TransitionSync(string runId, RunState newState)
    {
        using var conn = _factory.Open();
        TransitionInternal(conn, runId, newState);
    }

    private static void TransitionInternal(SqliteConnection conn, string runId, RunState newState)
    {
        var endedAt = newState is RunState.Completed or RunState.Cancelled or RunState.Succeeded or RunState.Failed
            ? System.DateTimeOffset.UtcNow.ToString("o")
            : null;
        conn.Execute(
            "UPDATE task_runs SET state = @State, ended_at = COALESCE(@EndedAt, ended_at), updated_at = @Now WHERE id = @Id",
            new { State = newState.ToString().ToLowerInvariant(), EndedAt = endedAt, Now = System.DateTimeOffset.UtcNow.ToString("o"), Id = runId });
    }

    public System.Threading.Tasks.Task SetCardPrimaryRunAsync(string cardId, string? runId, CardRepository cards)
    {
        if (_channel is null)
        {
            SetCardPrimaryRunSync(cardId, runId, cards);
            return System.Threading.Tasks.Task.CompletedTask;
        }
        return _channel.ExecuteAsync(conn => { SetCardPrimaryRunInternal(conn, cardId, runId, cards); return System.Threading.Tasks.Task.CompletedTask; });
    }

    private void SetCardPrimaryRunSync(string cardId, string? runId, CardRepository cards)
    {
        using var conn = _factory.Open();
        SetCardPrimaryRunInternal(conn, cardId, runId, cards);
    }

    private static void SetCardPrimaryRunInternal(SqliteConnection conn, string cardId, string? runId, CardRepository cards)
    {
        var card = cards.GetById(cardId);
        if (card is null) return;
        card.CurrentPrimaryRunId = runId;
        cards.UpdateAsync(card).Wait();
    }
}
