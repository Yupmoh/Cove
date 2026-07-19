using Cove.Persistence;
using Cove.Tasks.Runs;
using Cove.Tasks.Store;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Tasks.Tests;

public sealed class RunRepositoryTests : TasksTestBase
{
    private async System.Threading.Tasks.Task<(SqliteConnectionFactory factory, RunRepository runs, RunSegmentRepository segments, CardRepository cards, TasksWriteChannel channel)> NewAsync()
    {
        var fixture = CreateDatabase("cove-runs-");
        var factory = fixture.Factory;
        var store = fixture.Store;
        store.EnsureSchema();
        var channel = await fixture.StartChannelAsync();
        var runs = new RunRepository(factory, channel);
        var segments = new RunSegmentRepository(factory, channel);
        var cards = new CardRepository(factory, channel);
        SeedStatus(factory, "ws1", "todo");
        SeedStatus(factory, "ws1", "in-progress");
        return (factory, runs, segments, cards, channel);
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
    public async System.Threading.Tasks.Task CreateRun_SetsActiveStateAndFamilyId()
    {
        var (_, runs, _, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);

        var run = await runs.CreateAsync(cardId, "ws1", launchProfileJson: "{\"adapter\":\"claude\"}");

        Assert.Equal("active", run!.State);
        Assert.Equal(run.Id, run.RunFamilyId);
        Assert.Equal("{\"adapter\":\"claude\"}", run.LaunchProfileJson);
    }

    [Fact]
    public async System.Threading.Tasks.Task Transition_ActiveToCompleted_Succeeds()
    {
        var (_, runs, _, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        var run = await runs.CreateAsync(cardId, "ws1", null);

        await runs.TransitionAsync(run!.Id, RunState.Completed);

        var fetched = runs.GetById(run.Id);
        Assert.Equal("completed", fetched!.State);
        Assert.NotNull(fetched.EndedAt);
    }

    [Fact]
    public async System.Threading.Tasks.Task Transition_ActiveToInterrupted_Succeeds()
    {
        var (_, runs, _, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        var run = await runs.CreateAsync(cardId, "ws1", null);

        await runs.TransitionAsync(run!.Id, RunState.Interrupted);

        var fetched = runs.GetById(run.Id);
        Assert.Equal("interrupted", fetched!.State);
    }

    [Fact]
    public async System.Threading.Tasks.Task Transition_CompletedToActive_Rejected()
    {
        var (_, runs, _, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        var run = await runs.CreateAsync(cardId, "ws1", null);
        await runs.TransitionAsync(run!.Id, RunState.Completed);

        await Assert.ThrowsAsync<System.InvalidOperationException>(
            () => runs.TransitionAsync(run.Id, RunState.Active));
    }

    [Fact]
    public async System.Threading.Tasks.Task ListByCard_ReturnsAllRuns()
    {
        var (_, runs, _, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        await runs.CreateAsync(cardId, "ws1", null);
        await runs.CreateAsync(cardId, "ws1", null);

        var list = runs.ListByCard(cardId);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async System.Threading.Tasks.Task ListByFamily_GroupsRefires()
    {
        var (_, runs, _, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        var r1 = await runs.CreateAsync(cardId, "ws1", null);
        await runs.TransitionAsync(r1!.Id, RunState.Completed);
        var r2 = await runs.CreateAsync(cardId, "ws1", null, runFamilyId: r1.RunFamilyId);

        var family = runs.ListByFamily(r1.RunFamilyId);
        Assert.Equal(2, family.Count);
        Assert.Contains(family, r => r.Id == r1.Id);
        Assert.Contains(family, r => r.Id == r2!.Id);
    }

    [Fact]
    public async System.Threading.Tasks.Task ListByState_FiltersCorrectly()
    {
        var (_, runs, _, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        var r1 = await runs.CreateAsync(cardId, "ws1", null);
        var r2 = await runs.CreateAsync(cardId, "ws1", null);
        await runs.TransitionAsync(r1!.Id, RunState.Completed);

        var active = runs.ListByState("active");
        var completed = runs.ListByState("completed");
        Assert.Single(active);
        Assert.Single(completed);
    }

    [Fact]
    public async System.Threading.Tasks.Task AddSegment_AttachesToRun()
    {
        var (_, runs, segments, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        var run = await runs.CreateAsync(cardId, "ws1", null);

        var segment = await segments.AddAsync(run!.Id, nookId: "nook-1", adapterSessionId: "session-1");

        Assert.NotNull(segment);
        Assert.Equal(run.Id, segment!.RunId);
        Assert.Equal("nook-1", segment.NookId);
        var list = segments.ListByRun(run.Id);
        Assert.Single(list);
    }

    [Fact]
    public async System.Threading.Tasks.Task EndSegment_SetsEndedAt()
    {
        var (_, runs, segments, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        var run = await runs.CreateAsync(cardId, "ws1", null);
        var seg = await segments.AddAsync(run!.Id, "nook-1", "session-1");

        await segments.EndAsync(seg!.Id);

        var fetched = segments.ListByRun(run.Id)[0];
        Assert.NotNull(fetched.EndedAt);
    }

    [Fact]
    public async System.Threading.Tasks.Task ListByBay_ReturnsBayRuns()
    {
        var (_, runs, _, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        await runs.CreateAsync(cardId, "ws1", null);

        var list = runs.ListByBay("ws1");
        Assert.Single(list);
        Assert.Empty(runs.ListByBay("ws2"));
    }

    [Fact]
    public async System.Threading.Tasks.Task ListByCardAndState_ComposesFilters()
    {
        var (_, runs, _, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        var r1 = await runs.CreateAsync(cardId, "ws1", null);
        var r2 = await runs.CreateAsync(cardId, "ws1", null);
        await runs.TransitionAsync(r1!.Id, RunState.Completed);

        var active = runs.ListByCardAndState(cardId, "active");
        var completed = runs.ListByCardAndState(cardId, "completed");
        Assert.Single(active);
        Assert.Single(completed);
    }

    [Fact]
    public async System.Threading.Tasks.Task HasActiveRun_DetectsActiveOrInterrupted()
    {
        var (_, runs, _, cards, _) = await NewAsync();
        var cardId = await SeedCardAsync(cards, "ws1", 1);
        var r1 = await runs.CreateAsync(cardId, "ws1", null);

        Assert.True(runs.HasActiveRun(cardId));

        await runs.TransitionAsync(r1!.Id, RunState.Completed);
        Assert.False(runs.HasActiveRun(cardId));

        var r2 = await runs.CreateAsync(cardId, "ws1", null);
        await runs.TransitionAsync(r2!.Id, RunState.Interrupted);
        Assert.True(runs.HasActiveRun(cardId));
    }
}
