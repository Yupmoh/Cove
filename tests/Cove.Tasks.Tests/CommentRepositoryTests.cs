using Cove.Persistence;
using Cove.Tasks.Store;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Tasks.Tests;

public sealed class CommentRepositoryTests : TasksTestBase
{
    private async System.Threading.Tasks.Task<(SqliteConnectionFactory factory, CommentRepository comments, CardRepository cards, TasksWriteChannel channel)> NewAsync()
    {
        var fixture = CreateDatabase("cove-comments-");
        var factory = fixture.Factory;
        var store = fixture.Store;
        store.EnsureSchema();
        var channel = await fixture.StartChannelAsync();
        var comments = new CommentRepository(factory, channel);
        var cards = new CardRepository(factory, channel);
        SeedStatus(factory, "ws1", "todo");
        return (factory, comments, cards, channel);
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
    public async System.Threading.Tasks.Task Add_InsertsCommentWithKindAndSource()
    {
        var (_, comments, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);

        var row = await comments.AddAsync(cardId, "discussion", "## needs review", "user:moh");

        Assert.NotNull(row);
        Assert.Equal("discussion", row!.Kind);
        Assert.Equal("## needs review", row.Body);
        Assert.Equal("user:moh", row.Source);
    }

    [Fact]
    public async System.Threading.Tasks.Task Add_RejectsSystemEventKind()
    {
        var (_, comments, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);

        await Assert.ThrowsAsync<System.ArgumentException>(() => comments.AddAsync(cardId, "system_event", "auto", "system"));
    }

    [Fact]
    public async System.Threading.Tasks.Task ListByCard_ReturnsOrderedByCreatedAt()
    {
        var (factory, comments, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        await comments.AddAsync(cardId, "discussion", "first", "user:a");
        await comments.AddAsync(cardId, "instruction", "second", "user:b");
        using (var connection = factory.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE comments SET created_at = '2026-01-01T00:00:00.0000000+00:00' WHERE card_id = @CardId";
            command.Parameters.AddWithValue("@CardId", cardId);
            command.ExecuteNonQuery();
        }

        var list = comments.ListByCard(cardId);
        Assert.Equal(2, list.Count);
        Assert.Equal("first", list[0].Body);
        Assert.Equal("second", list[1].Body);
    }

    [Fact]
    public async System.Threading.Tasks.Task Count_ReturnsCommentCount()
    {
        var (_, comments, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        await comments.AddAsync(cardId, "discussion", "a", "user:a");
        await comments.AddAsync(cardId, "discussion", "b", "user:a");

        Assert.Equal(2, comments.CountByCard(cardId));
    }

    [Fact]
    public async System.Threading.Tasks.Task Delete_RemovesComment()
    {
        var (_, comments, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        var row = await comments.AddAsync(cardId, "discussion", "temp", "user:a");

        await comments.DeleteAsync(row!.Id);

        Assert.Equal(0, comments.CountByCard(cardId));
    }

    [Fact]
    public async System.Threading.Tasks.Task CardDelete_CascadesComments()
    {
        var (_, comments, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        await comments.AddAsync(cardId, "discussion", "a", "user:a");
        await comments.AddAsync(cardId, "discussion", "b", "user:a");

        await cards.DeleteAsync(cardId);

        Assert.Equal(0, comments.CountByCard(cardId));
    }

    [Fact]
    public async System.Threading.Tasks.Task Add_SyncsCommentIdsJsonOnCard()
    {
        var (factory, comments, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        var c1 = await comments.AddAsync(cardId, "discussion", "first", "user:a");
        var c2 = await comments.AddAsync(cardId, "discussion", "second", "user:a");

        var card = cards.GetById(cardId);
        Assert.Contains(c1!.Id, card!.CommentIdsJson);
        Assert.Contains(c2!.Id, card.CommentIdsJson);
    }
}
