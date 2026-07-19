using Cove.Persistence;
using Cove.Tasks.Store;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Tasks.Tests;

public sealed class CardRepositoryTests : TasksTestBase
{
    private async System.Threading.Tasks.Task<(SqliteConnectionFactory factory, TasksStore store, TasksWriteChannel channel, TaskCounterRepository counter, CardRepository cards)> NewAsync()
    {
        var fixture = CreateDatabase("cove-cards-");
        var factory = fixture.Factory;
        var store = fixture.Store;
        store.EnsureSchema();
        SeedStatus(factory, "ws1", "todo");
        SeedStatus(factory, "ws1", "in-progress");
        SeedStatus(factory, "ws2", "todo");
        var channel = await fixture.StartChannelAsync();
        var counter = new TaskCounterRepository(factory, channel);
        var cards = new CardRepository(factory, channel);
        return (factory, store, channel, counter, cards);
    }

    private static void SeedStatus(SqliteConnectionFactory factory, string bayId, string id)
    {
        using var conn = factory.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO statuses (bay_id, id, name, hex_color, position, created_at, updated_at) VALUES (@BayId, @Id, @Id, '808080', 0, @Now, @Now)";
        cmd.Parameters.AddWithValue("@BayId", bayId);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Now", System.DateTimeOffset.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    private static CardRow Card(string ws, int num, string title, double orderKey, string status = "todo") => new()
    {
        Id = System.Guid.NewGuid().ToString("N"),
        BayId = ws,
        TaskNumber = num,
        Title = title,
        StatusId = status,
        Source = "user:test",
        OrderKey = orderKey,
        CreatedAt = System.DateTimeOffset.UtcNow.ToString("o"),
        UpdatedAt = System.DateTimeOffset.UtcNow.ToString("o"),
    };

    [Fact]
    public async System.Threading.Tasks.Task InsertAndGet_RoundTripsAllFields()
    {
        var (_, _, _, _, cards) = await NewAsync();
        var row = Card("ws1", 1, "test card", 100.0);
        row.Description = "## markdown body";
        row.Priority = 3;
        row.Size = 4;
        row.Assignee = "user:moh";
        row.Source = "user:moh";
        row.CurrentPrimaryRunId = "run-1";
        row.LaunchConfigJson = "{\"adapter\":\"claude\"}";
        row.AgentRef = "agent:claude";
        row.ProfileSlug = "default";
        row.DueAt = "2026-08-01T00:00:00Z";
        row.CommentIdsJson = "[\"c1\",\"c2\"]";

        await cards.InsertAsync(row);
        var fetched = cards.GetById(row.Id);

        Assert.NotNull(fetched);
        Assert.Equal("test card", fetched!.Title);
        Assert.Equal("## markdown body", fetched.Description);
        Assert.Equal(3, fetched.Priority);
        Assert.Equal(4, fetched.Size);
        Assert.Equal("user:moh", fetched.Assignee);
        Assert.Equal("user:moh", fetched.Source);
        Assert.Equal("run-1", fetched.CurrentPrimaryRunId);
        Assert.Equal("{\"adapter\":\"claude\"}", fetched.LaunchConfigJson);
        Assert.Equal("agent:claude", fetched.AgentRef);
        Assert.Equal("default", fetched.ProfileSlug);
        Assert.Equal("2026-08-01T00:00:00Z", fetched.DueAt);
        Assert.Equal("[\"c1\",\"c2\"]", fetched.CommentIdsJson);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetByBayAndNumber_ReturnsCard()
    {
        var (_, _, _, _, cards) = await NewAsync();
        var row = Card("ws1", 42, "find me", 0);
        await cards.InsertAsync(row);

        var fetched = cards.GetByBayAndNumber("ws1", 42);
        Assert.NotNull(fetched);
        Assert.Equal("find me", fetched!.Title);
    }

    [Fact]
    public async System.Threading.Tasks.Task ListByStatus_OrdersByOrderKey()
    {
        var (_, _, _, _, cards) = await NewAsync();
        await cards.InsertAsync(Card("ws1", 1, "c", 300.0, "todo"));
        await cards.InsertAsync(Card("ws1", 2, "a", 100.0, "todo"));
        await cards.InsertAsync(Card("ws1", 3, "b", 200.0, "todo"));

        var list = cards.ListByStatus("ws1", "todo");
        Assert.Equal(new[] { "a", "b", "c" }, list.Select(c => c.Title).ToArray());
    }

    [Fact]
    public async System.Threading.Tasks.Task ListByStatus_IsolatesByBay()
    {
        var (_, _, _, _, cards) = await NewAsync();
        await cards.InsertAsync(Card("ws1", 1, "ws1-card", 100.0, "todo"));
        await cards.InsertAsync(Card("ws2", 1, "ws2-card", 200.0, "todo"));

        var ws1 = cards.ListByStatus("ws1", "todo");
        Assert.Single(ws1);
        Assert.Equal("ws1-card", ws1[0].Title);

        var ws2 = cards.ListByStatus("ws2", "todo");
        Assert.Single(ws2);
        Assert.Equal("ws2-card", ws2[0].Title);
    }

    [Fact]
    public async System.Threading.Tasks.Task Update_ChangesFields()
    {
        var (_, _, _, _, cards) = await NewAsync();
        var row = Card("ws1", 1, "original", 0);
        await cards.InsertAsync(row);

        row.Title = "updated";
        row.StatusId = "in-progress";
        row.Priority = 0;
        row.OrderKey = 500.0;
        var affected = await cards.UpdateAsync(row);

        Assert.Equal(1, affected);
        var fetched = cards.GetById(row.Id);
        Assert.Equal("updated", fetched!.Title);
        Assert.Equal("in-progress", fetched.StatusId);
        Assert.Equal(0, fetched.Priority);
        Assert.Equal(500.0, fetched.OrderKey);
    }

    [Fact]
    public async System.Threading.Tasks.Task Delete_RemovesCard()
    {
        var (_, _, _, _, cards) = await NewAsync();
        var row = Card("ws1", 1, "delete me", 0);
        await cards.InsertAsync(row);

        var affected = await cards.DeleteAsync(row.Id);
        Assert.Equal(1, affected);
        Assert.Null(cards.GetById(row.Id));
    }

    [Fact]
    public async System.Threading.Tasks.Task MoveCardToPosition_UsesFractionalOrderKey()
    {
        var (_, _, _, _, cards) = await NewAsync();
        var first = Card("ws1", 1, "first", 100.0, "todo");
        var second = Card("ws1", 2, "second", 200.0, "todo");
        var third = Card("ws1", 3, "third", 300.0, "todo");
        await cards.InsertAsync(first);
        await cards.InsertAsync(second);
        await cards.InsertAsync(third);

        await cards.MoveToPositionAsync("ws1", "todo", third.Id, beforeId: first.Id);

        var list = cards.ListByStatus("ws1", "todo");
        Assert.Equal("third", list[0].Title);
        Assert.Equal("first", list[1].Title);
        Assert.Equal("second", list[2].Title);
        Assert.True(list[0].OrderKey < list[1].OrderKey);
        Assert.True(list[1].OrderKey < list[2].OrderKey);
    }

    [Fact]
    public async System.Threading.Tasks.Task MoveCardToPosition_DoesNotAffectOtherBay()
    {
        var (_, _, _, _, cards) = await NewAsync();
        var ws1Card = Card("ws1", 1, "ws1-a", 100.0, "todo");
        var ws2Card = Card("ws2", 1, "ws2-a", 100.0, "todo");
        await cards.InsertAsync(ws1Card);
        await cards.InsertAsync(ws2Card);

        var ws2Before = cards.ListByStatus("ws2", "todo")[0].OrderKey;
        await cards.MoveToPositionAsync("ws1", "todo", ws1Card.Id, beforeId: null);
        var ws2After = cards.ListByStatus("ws2", "todo")[0].OrderKey;

        Assert.Equal(ws2Before, ws2After);
    }

    [Fact]
    public async System.Threading.Tasks.Task NewCardLandsAtColumnTop_BelowCurrentMinimum()
    {
        var (_, _, _, _, cards) = await NewAsync();
        await cards.InsertAsync(Card("ws1", 1, "existing", 100.0, "todo"));

        var newKey = await cards.NextOrderKeyAsync("ws1", "todo");
        await cards.InsertAsync(Card("ws1", 2, "new", newKey, "todo"));

        var list = cards.ListByStatus("ws1", "todo");
        Assert.Equal("new", list[0].Title);
        Assert.True(list[0].OrderKey < list[1].OrderKey);
    }

    [Fact]
    public async System.Threading.Tasks.Task NextOrderKey_IsolatesByBay()
    {
        var (_, _, _, _, cards) = await NewAsync();
        await cards.InsertAsync(Card("ws1", 1, "ws1-card", 100.0, "todo"));

        var ws2Key = await cards.NextOrderKeyAsync("ws2", "todo");
        Assert.Equal(0.0, ws2Key);
    }
}
