using Cove.Persistence;
using Cove.Tasks.Store;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Tasks.Tests;

public sealed class LabelRepositoryTests
{
    private static string NewDb() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-labels-" + System.Guid.NewGuid().ToString("N") + ".db");

    private static async System.Threading.Tasks.Task<(SqliteConnectionFactory factory, TasksStore store, LabelRepository labels, CardRepository cards, TasksWriteChannel channel)> NewAsync()
    {
        var factory = new SqliteConnectionFactory(NewDb());
        var store = new TasksStore(factory, NullLogger.Instance);
        store.EnsureSchema();
        var channel = new TasksWriteChannel(factory);
        await channel.StartAsync();
        var labels = new LabelRepository(factory, channel);
        var cards = new CardRepository(factory, channel);
        SeedStatus(factory, "ws1", "todo");
        return (factory, store, labels, cards, channel);
    }

    private static void SeedStatus(SqliteConnectionFactory factory, string ws, string id)
    {
        using var conn = factory.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO statuses (bay_id, id, name, hex_color, position, created_at, updated_at) VALUES (@Ws, @Id, @Id, '808080', 0, @Now, @Now)";
        cmd.Parameters.AddWithValue("@Ws", ws);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Now", System.DateTimeOffset.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    private static async System.Threading.Tasks.Task<string> SeedCardAsync(CardRepository cards, string ws, int num)
    {
        var row = new CardRow
        {
            Id = System.Guid.NewGuid().ToString("N"),
            BayId = ws,
            TaskNumber = num,
            Title = "card-" + num,
            StatusId = "todo",
            Source = "user:test",
            OrderKey = num * 100,
            CreatedAt = System.DateTimeOffset.UtcNow.ToString("o"),
            UpdatedAt = System.DateTimeOffset.UtcNow.ToString("o"),
        };
        await cards.InsertAsync(row);
        return row.Id;
    }

    [Fact]
    public async System.Threading.Tasks.Task Create_InsertsLabelWithFields()
    {
        var (_, _, labels, _, _) = await NewAsync();
        var row = await labels.CreateAsync("ws1", "bug", "Bug", "ff0000", position: 0);
        Assert.NotNull(row);
        Assert.Equal("bug", row!.Id);
        Assert.Equal("Bug", row.Name);
        Assert.Equal("ff0000", row.HexColor);
    }

    [Fact]
    public async System.Threading.Tasks.Task ListByBay_ReturnsOrderedByPosition()
    {
        var (_, _, labels, _, _) = await NewAsync();
        await labels.CreateAsync("ws1", "c", "C", "808080", position: 2);
        await labels.CreateAsync("ws1", "a", "A", "808080", position: 0);
        await labels.CreateAsync("ws1", "b", "B", "808080", position: 1);

        var list = labels.ListByBay("ws1");
        Assert.Equal(new[] { "a", "b", "c" }, list.Select(l => l.Id).ToArray());
    }

    [Fact]
    public async System.Threading.Tasks.Task Delete_RemovesLabelAndCascadesCardLabels()
    {
        var (_, _, labels, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        await labels.CreateAsync("ws1", "bug", "Bug", "ff0000", position: 0);
        await labels.AssignToCardAsync(cardId, "bug");

        await labels.DeleteAsync("ws1", "bug");

        Assert.Null(labels.GetByBayAndId("ws1", "bug"));
        Assert.Empty(labels.GetLabelsForCard(cardId));
    }

    [Fact]
    public async System.Threading.Tasks.Task AssignAndUnassign_CardLabel()
    {
        var (_, _, labels, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        await labels.CreateAsync("ws1", "bug", "Bug", "ff0000", position: 0);
        await labels.CreateAsync("ws1", "feature", "Feature", "00ff00", position: 1);

        await labels.AssignToCardAsync(cardId, "bug");
        await labels.AssignToCardAsync(cardId, "feature");

        var cardLabels = labels.GetLabelsForCard(cardId);
        Assert.Equal(2, cardLabels.Count);

        await labels.UnassignFromCardAsync(cardId, "bug");
        var after = labels.GetLabelsForCard(cardId);
        Assert.Single(after);
        Assert.Equal("feature", after[0].Id);
    }

    [Fact]
    public async System.Threading.Tasks.Task FilterCardsByLabel_ReturnsOnlyMatchingCards()
    {
        var (_, _, labels, cards, _) = await NewAsync();
        var card1 = await SeedCardAsync(cards, "ws1", 1);
        var card2 = await SeedCardAsync(cards, "ws1", 2);
        var card3 = await SeedCardAsync(cards, "ws1", 3);
        await labels.CreateAsync("ws1", "bug", "Bug", "ff0000", position: 0);
        await labels.AssignToCardAsync(card1, "bug");
        await labels.AssignToCardAsync(card3, "bug");

        var filtered = labels.FilterCardsByLabel("ws1", "bug");
        Assert.Equal(2, filtered.Count);
        Assert.Contains(card1, filtered);
        Assert.Contains(card3, filtered);
        Assert.DoesNotContain(card2, filtered);
    }

    [Fact]
    public async System.Threading.Tasks.Task CardDelete_CascadesCardLabels()
    {
        var (_, _, labels, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        await labels.CreateAsync("ws1", "bug", "Bug", "ff0000", position: 0);
        await labels.AssignToCardAsync(cardId, "bug");
        Assert.Single(labels.GetLabelsForCard(cardId));

        await cards.DeleteAsync(cardId);

        Assert.Empty(labels.GetLabelsForCard(cardId));
    }

    [Fact]
    public async System.Threading.Tasks.Task Reorder_UpdatesPositions()
    {
        var (_, _, labels, _, _) = await NewAsync();
        await labels.CreateAsync("ws1", "a", "A", "808080", position: 0);
        await labels.CreateAsync("ws1", "b", "B", "808080", position: 1);
        await labels.CreateAsync("ws1", "c", "C", "808080", position: 2);

        await labels.ReorderAsync("ws1", new[] { "c", "a", "b" });

        var list = labels.ListByBay("ws1");
        Assert.Equal(new[] { "c", "a", "b" }, list.Select(l => l.Id).ToArray());
    }

    [Fact]
    public async System.Threading.Tasks.Task GetLabelsForCard_DoesNotMatchLabelsFromOtherBay()
    {
        var (factory, _, labels, cards, _) = await NewAsync();
        SeedStatus(factory, "ws2", "todo");
        var card1 = await SeedCardAsync(cards, "ws1", 1);
        var card2 = await SeedCardAsync(cards, "ws2", 2);
        await labels.CreateAsync("ws1", "bug", "Bug", "ff0000", position: 0);
        await labels.CreateAsync("ws2", "bug", "Bug", "00ff00", position: 0);
        await labels.AssignToCardAsync(card1, "bug");

        var card1Labels = labels.GetLabelsForCard(card1);
        Assert.Single(card1Labels);
        Assert.Equal("ws1", card1Labels[0].BayId);
        Assert.Equal("ff0000", card1Labels[0].HexColor);
    }

    [Fact]
    public async System.Threading.Tasks.Task Delete_DoesNotRemoveCardLabelsFromOtherBay()
    {
        var (factory, _, labels, cards, _) = await NewAsync();
        SeedStatus(factory, "ws2", "todo");
        var card1 = await SeedCardAsync(cards, "ws1", 1);
        var card2 = await SeedCardAsync(cards, "ws2", 2);
        await labels.CreateAsync("ws1", "bug", "Bug", "ff0000", position: 0);
        await labels.CreateAsync("ws2", "bug", "Bug", "00ff00", position: 0);
        await labels.AssignToCardAsync(card1, "bug");
        await labels.AssignToCardAsync(card2, "bug");

        await labels.DeleteAsync("ws1", "bug");

        Assert.Empty(labels.GetLabelsForCard(card1));
        var ws2Labels = labels.GetLabelsForCard(card2);
        Assert.Single(ws2Labels);
        Assert.Equal("ws2", ws2Labels[0].BayId);
    }

    [Fact]
    public async System.Threading.Tasks.Task BayIsolation_LabelsScopedPerBay()
    {
        var (_, _, labels, _, _) = await NewAsync();
        await labels.CreateAsync("ws1", "bug", "Bug", "ff0000", position: 0);
        await labels.CreateAsync("ws2", "bug", "Bug", "ff0000", position: 0);

        var ws1 = labels.ListByBay("ws1");
        var ws2 = labels.ListByBay("ws2");
        Assert.Single(ws1);
        Assert.Single(ws2);
    }
}
