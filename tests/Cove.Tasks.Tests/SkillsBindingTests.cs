using Cove.Persistence;
using Cove.Tasks.Store;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Tasks.Tests;

public sealed class SkillsBindingTests
{
    private static string NewDb() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-binding-" + System.Guid.NewGuid().ToString("N") + ".db");

    private static async System.Threading.Tasks.Task<(SqliteConnectionFactory factory, CardRepository cards, TasksWriteChannel channel, TaskCounterRepository counter)> NewAsync()
    {
        var factory = new SqliteConnectionFactory(NewDb());
        var store = new TasksStore(factory, NullLogger.Instance);
        store.EnsureSchema();
        var channel = new TasksWriteChannel(factory);
        await channel.StartAsync();
        var cards = new CardRepository(factory, channel);
        var counter = new TaskCounterRepository(factory, channel);
        SeedStatus(factory, "ws1", "todo");
        return (factory, cards, channel, counter);
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
    public async System.Threading.Tasks.Task SetBinding_PersistsAgentRefAndSkillSelection()
    {
        var (_, cards, _, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);

        var skills = new[] { new SkillSelection("builtin", "code-review", "auto"), new SkillSelection("user", "debug", "manual") };
        await SkillsBinder.BindAsync(cards, cardId, agentRef: "agent:claude", skills, profileSlug: "default");

        var card = cards.GetById(cardId);
        Assert.Equal("agent:claude", card!.AgentRef);
        Assert.Equal("default", card.ProfileSlug);
        var parsed = SkillsBinder.ParseSkillSelection(card.SkillSelectionJson);
        Assert.Equal(2, parsed.Count);
        Assert.Equal("code-review", parsed[0].Name);
        Assert.Equal("auto", parsed[0].Mode);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetBinding_ReturnsBoundData()
    {
        var (_, cards, _, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        var skills = new[] { new SkillSelection("builtin", "test-writer", "auto") };
        await SkillsBinder.BindAsync(cards, cardId, "agent:codex", skills, "dev");

        var binding = SkillsBinder.GetBinding(cards, cardId);
        Assert.Equal("agent:codex", binding.AgentRef);
        Assert.Equal("dev", binding.ProfileSlug);
        Assert.Single(binding.Skills);
        Assert.Equal("test-writer", binding.Skills[0].Name);
    }

    [Fact]
    public async System.Threading.Tasks.Task ResolveTaskProfile_ReturnsSessionStartPayload()
    {
        var (_, cards, _, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        var skills = new[] { new SkillSelection("builtin", "code-review", "auto") };
        await SkillsBinder.BindAsync(cards, cardId, "agent:claude", skills, "default");

        var card = cards.GetById(cardId)!;
        var payload = SkillsBinder.ResolveTaskProfile(card);

        Assert.NotNull(payload);
        Assert.Equal("agent:claude", payload.AgentRef);
        Assert.Equal("default", payload.ProfileSlug);
        Assert.Single(payload.Skills);
        Assert.Equal("code-review", payload.Skills[0].Name);
    }

    [Fact]
    public async System.Threading.Tasks.Task ResolveTaskProfile_CardWithNoBinding_ReturnsEmpty()
    {
        var (_, cards, _, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);

        var card = cards.GetById(cardId)!;
        var payload = SkillsBinder.ResolveTaskProfile(card);

        Assert.NotNull(payload);
        Assert.Null(payload.AgentRef);
        Assert.Null(payload.ProfileSlug);
        Assert.Empty(payload.Skills);
    }
}
