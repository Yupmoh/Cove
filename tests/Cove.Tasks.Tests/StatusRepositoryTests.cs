using Cove.Persistence;
using Cove.Tasks.Store;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Tasks.Tests;

public sealed class StatusRepositoryTests : TasksTestBase
{
    private async System.Threading.Tasks.Task<(SqliteConnectionFactory factory, TasksStore store, StatusRepository statuses, CardRepository cards, TaskCounterRepository counter, TasksWriteChannel channel)> NewAsync()
    {
        var fixture = CreateDatabase("cove-status-");
        var factory = fixture.Factory;
        var store = fixture.Store;
        store.EnsureSchema();
        var channel = await fixture.StartChannelAsync();
        var statuses = new StatusRepository(factory, channel);
        var cards = new CardRepository(factory, channel);
        var counter = new TaskCounterRepository(factory, channel);
        return (factory, store, statuses, cards, counter, channel);
    }

    private static void SeedCard(SqliteConnectionFactory factory, string ws, int num, string statusId, double orderKey)
    {
        using var conn = factory.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO cards (id, bay_id, task_number, title, status_id, source, order_key, created_at, updated_at) VALUES (@Id, @Ws, @Num, @Title, @Status, @Source, @OrderKey, @Now, @Now)";
        cmd.Parameters.AddWithValue("@Id", System.Guid.NewGuid().ToString("N"));
        cmd.Parameters.AddWithValue("@Ws", ws);
        cmd.Parameters.AddWithValue("@Num", num);
        cmd.Parameters.AddWithValue("@Title", "card-" + num);
        cmd.Parameters.AddWithValue("@Status", statusId);
        cmd.Parameters.AddWithValue("@Source", "user:test");
        cmd.Parameters.AddWithValue("@OrderKey", orderKey);
        cmd.Parameters.AddWithValue("@Now", System.DateTimeOffset.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async System.Threading.Tasks.Task Create_InsertsStatusWithFields()
    {
        var (_, _, statuses, _, _, _) = await NewAsync();
        var row = await statuses.CreateAsync("ws1", "backlog", "Backlog", "808080", position: 10);
        Assert.NotNull(row);
        Assert.Equal("backlog", row!.Id);
        Assert.Equal("Backlog", row.Name);
        Assert.Equal(10, row.Position);
        Assert.False(row.Hidden);
    }

    [Fact]
    public async System.Threading.Tasks.Task ListByBay_ReturnsOrderedByPosition()
    {
        var (_, _, statuses, _, _, _) = await NewAsync();
        await statuses.CreateAsync("ws1", "done", "Done", "34c759", position: 30);
        await statuses.CreateAsync("ws1", "todo", "Todo", "808080", position: 10);
        await statuses.CreateAsync("ws1", "in-progress", "In Progress", "4a9eff", position: 20);

        var list = statuses.ListByBay("ws1");
        Assert.Equal(new[] { "todo", "in-progress", "done" }, list.Select(s => s.Id).ToArray());
    }

    [Fact]
    public async System.Threading.Tasks.Task ListByBay_ExcludesHiddenByDefault()
    {
        var (_, _, statuses, _, _, _) = await NewAsync();
        await statuses.CreateAsync("ws1", "visible", "Visible", "808080", position: 0);
        var hidden = await statuses.CreateAsync("ws1", "archived", "Archived", "999999", position: 10);
        await statuses.SetHiddenAsync("ws1", "archived", hidden: true);

        var visible = statuses.ListByBay("ws1", includeHidden: false);
        Assert.Single(visible);
        Assert.Equal("visible", visible[0].Id);

        var all = statuses.ListByBay("ws1", includeHidden: true);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async System.Threading.Tasks.Task Reorder_UpdatesPositionsFractionally()
    {
        var (_, _, statuses, _, _, _) = await NewAsync();
        var a = await statuses.CreateAsync("ws1", "a", "A", "808080", position: 0);
        var b = await statuses.CreateAsync("ws1", "b", "B", "808080", position: 1);
        var c = await statuses.CreateAsync("ws1", "c", "C", "808080", position: 2);

        await statuses.ReorderAsync("ws1", new[] { "c", "a", "b" });

        var list = statuses.ListByBay("ws1");
        Assert.Equal(new[] { "c", "a", "b" }, list.Select(s => s.Id).ToArray());
        Assert.True(list[0].Position < list[1].Position);
        Assert.True(list[1].Position < list[2].Position);
    }

    [Fact]
    public async System.Threading.Tasks.Task Delete_WithCards_RehomesToTargetStatus()
    {
        var (factory, _, statuses, cards, counter, _) = await NewAsync();
        await statuses.CreateAsync("ws1", "todo", "Todo", "808080", position: 0);
        await statuses.CreateAsync("ws1", "backlog", "Backlog", "808080", position: 1);
        var cardRow = new CardRow
        {
            Id = System.Guid.NewGuid().ToString("N"),
            BayId = "ws1",
            TaskNumber = 1,
            Title = "test",
            StatusId = "backlog",
            Source = "user:test",
            OrderKey = 0,
            CreatedAt = System.DateTimeOffset.UtcNow.ToString("o"),
            UpdatedAt = System.DateTimeOffset.UtcNow.ToString("o"),
        };
        await cards.InsertAsync(cardRow);

        await statuses.DeleteAsync("ws1", "backlog", rehomeToStatusId: "todo");

        var card = cards.GetById(cardRow.Id);
        Assert.Equal("todo", card!.StatusId);
        Assert.Null(statuses.GetByBayAndId("ws1", "backlog"));
    }

    [Fact]
    public async System.Threading.Tasks.Task Delete_WithCardsAndNoRehome_BlocksDeletion()
    {
        var (factory, _, statuses, cards, _, _) = await NewAsync();
        await statuses.CreateAsync("ws1", "todo", "Todo", "808080", position: 0);
        await statuses.CreateAsync("ws1", "backlog", "Backlog", "808080", position: 1);
        SeedCard(factory, "ws1", 1, "backlog", 0);

        var ex = await Assert.ThrowsAsync<System.InvalidOperationException>(
            () => statuses.DeleteAsync("ws1", "backlog", rehomeToStatusId: null));
        Assert.Contains("backlog", ex.Message);
    }

    [Fact]
    public async System.Threading.Tasks.Task BayIsolation_StatusesScopedPerBay()
    {
        var (_, _, statuses, _, _, _) = await NewAsync();
        await statuses.CreateAsync("ws1", "todo", "Todo", "808080", position: 0);
        await statuses.CreateAsync("ws2", "todo", "Todo", "808080", position: 0);

        var ws1 = statuses.ListByBay("ws1");
        var ws2 = statuses.ListByBay("ws2");
        Assert.Single(ws1);
        Assert.Single(ws2);
        Assert.Equal("ws1", ws1[0].BayId);
        Assert.Equal("ws2", ws2[0].BayId);
    }
}
