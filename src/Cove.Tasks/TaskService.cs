using Cove.Persistence;
using Cove.Tasks.Store;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Tasks;

public sealed class TaskService
{
    private readonly SqliteConnectionFactory _factory;
    private readonly TasksWriteChannel _channel;
    private readonly CardRepository _cards;
    private readonly TaskCounterRepository _counter;
    private readonly TasksStore _store;

    public TaskService(string dataDir, ILogger logger)
    {
        var dbPath = System.IO.Path.Combine(dataDir, "tasks.db");
        _factory = new SqliteConnectionFactory(dbPath);
        _store = new TasksStore(_factory, logger);
        _store.EnsureSchema();
        _channel = new TasksWriteChannel(_factory, logger);
        _cards = new CardRepository(_factory, _channel);
        _counter = new TaskCounterRepository(_factory, _channel);
    }

    public TaskService(SqliteConnectionFactory factory, TasksWriteChannel channel, TasksStore store, CardRepository cards, TaskCounterRepository counter)
    {
        _factory = factory;
        _channel = channel;
        _store = store;
        _cards = cards;
        _counter = counter;
    }

    public System.Threading.Tasks.Task StartAsync() => _channel.StartAsync();

    public void SeedDefaultStatuses(string workspaceId)
    {
        var now = System.DateTimeOffset.UtcNow.ToString("o");
        var defaults = new[]
        {
            ("todo", "Todo", "808080", 0, 0, 0, 0, 0),
            ("in-progress", "In Progress", "4a9eff", 1, 1, 0, 0, 0),
            ("in-review", "In Review", "f5a623", 2, 0, 0, 1, 0),
            ("done", "Done", "34c759", 3, 0, 0, 0, 1),
            ("looping", "Looping", "9b59b6", 4, 0, 1, 0, 0),
        };
        using var conn = _factory.Open();
        foreach (var (id, name, color, pos, isProg, isLoop, isReview, isDone) in defaults)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO statuses (workspace_id, id, name, hex_color, position, is_in_progress, is_looping, is_review, is_completion, created_at, updated_at) VALUES (@WorkspaceId, @Id, @Name, @Color, @Pos, @IsProg, @IsLoop, @IsReview, @IsDone, @Now, @Now)";
            cmd.Parameters.AddWithValue("@WorkspaceId", workspaceId);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Name", name);
            cmd.Parameters.AddWithValue("@Color", color);
            cmd.Parameters.AddWithValue("@Pos", pos);
            cmd.Parameters.AddWithValue("@IsProg", isProg);
            cmd.Parameters.AddWithValue("@IsLoop", isLoop);
            cmd.Parameters.AddWithValue("@IsReview", isReview);
            cmd.Parameters.AddWithValue("@IsDone", isDone);
            cmd.Parameters.AddWithValue("@Now", now);
            cmd.ExecuteNonQuery();
        }
    }

    public async System.Threading.Tasks.Task<CardRow> CreateCardAsync(string workspaceId, string title, string source, string? description, int priority, int size, string? assignee, string statusId = "todo")
    {
        SeedDefaultStatuses(workspaceId);
        var number = await _counter.NextNumberAsync(workspaceId);
        var orderKey = await _cards.NextOrderKeyAsync(workspaceId, statusId);
        var now = System.DateTimeOffset.UtcNow.ToString("o");
        var row = new CardRow
        {
            Id = System.Guid.NewGuid().ToString("N"),
            WorkspaceId = workspaceId,
            TaskNumber = number,
            Title = title,
            Description = description ?? "",
            StatusId = statusId,
            Priority = priority,
            Size = size,
            Assignee = assignee,
            Source = source,
            OrderKey = orderKey,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await _cards.InsertAsync(row);
        return row;
    }

    public CardRow? GetCard(string id) => _cards.GetById(id);
    public CardRow? GetCardByHumanId(string workspaceId, int number) => _cards.GetByWorkspaceAndNumber(workspaceId, number);
    public IReadOnlyList<CardRow> ListCards(string workspaceId) => _cards.ListByWorkspace(workspaceId);
    public IReadOnlyList<CardRow> ListCardsByStatus(string workspaceId, string statusId) => _cards.ListByStatus(workspaceId, statusId);

    public async System.Threading.Tasks.Task<int> UpdateCardAsync(CardRow row)
    {
        row.UpdatedAt = System.DateTimeOffset.UtcNow.ToString("o");
        return await _cards.UpdateAsync(row);
    }

    public async System.Threading.Tasks.Task<int> DeleteCardAsync(string id) => await _cards.DeleteAsync(id);
}
