using System.Text.Json;
using Cove.Engine.Bays;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BayModelTests
{
    private static BayModel Bay(IReadOnlyDictionary<string, NookRecord>? nooks = null) =>
        new()
        {
            Id = "ws1",
            Name = "proj",
            ProjectDir = "/tmp/proj",
            Nooks = nooks ?? new Dictionary<string, NookRecord>(),
        };

    [Fact]
    public void BayModel_RoundTrips()
    {
        var m = Bay(new Dictionary<string, NookRecord> { ["p1"] = new NookRecord { NookId = "p1", Name = "term" } });
        var json = JsonSerializer.Serialize(m, BaysJsonContext.Default.BayModel);
        var back = JsonSerializer.Deserialize(json, BaysJsonContext.Default.BayModel)!;
        Assert.Equal("ws1", back.Id);
        Assert.Equal("proj", back.Name);
        Assert.True(back.Nooks.ContainsKey("p1"));
        Assert.Equal("term", back.Nooks["p1"].Name);
    }

    [Fact]
    public async Task Fuzz_ConcurrentMutations_NoLostUpdates()
    {
        await using var actor = new Actor<BayModel>(Bay());
        int n = 0;
        var tasks = Enumerable.Range(0, 50).Select(_ => actor.Mutate(m =>
        {
            var id = $"n-{Interlocked.Increment(ref n)}";
            var nooks = new Dictionary<string, NookRecord>(m.Nooks) { [id] = new NookRecord { NookId = id } };
            return m with { Nooks = nooks };
        })).ToArray();
        await Task.WhenAll(tasks);
        Assert.Equal(50, actor.State.Nooks.Count);
    }

    [Fact]
    public void RegistryModel_StateJson_RoundTrips()
    {
        var reg = new RegistryModel
        {
            FocusedBayId = "ws1",
            OpenBays = ["ws1", "ws2"],
            Collections = [new Collection { Id = "c1", Name = "client-a" }],
            ActiveCollectionId = "c1",
        };
        var json = JsonSerializer.Serialize(reg, BaysJsonContext.Default.RegistryModel);
        var back = JsonSerializer.Deserialize(json, BaysJsonContext.Default.RegistryModel)!;
        Assert.Equal("ws1", back.FocusedBayId);
        Assert.Equal(2, back.OpenBays.Count);
        Assert.Single(back.Collections);
        Assert.Equal("client-a", back.Collections[0].Name);
        Assert.Equal("c1", back.ActiveCollectionId);
    }

    [Fact]
    public async Task Actor_ConcurrentMutations_NeverLosesUpdates_NeverCorrupts()
    {
        var seed = new Dictionary<string, NookRecord>
        {
            ["p1"] = new NookRecord { NookId = "p1" },
            ["p2"] = new NookRecord { NookId = "p2" },
            ["p3"] = new NookRecord { NookId = "p3" },
        };
        var actor = new Actor<BayModel>(Bay(seed));
        var ids = new[] { "p1", "p2", "p3" };
        var tasks = new List<Task>();
        for (int i = 0; i < 200; i++)
        {
            int n = i;
            tasks.Add(actor.Mutate(m =>
            {
                var id = ids[n % ids.Length];
                var nooks = new Dictionary<string, NookRecord>(m.Nooks);
                nooks[id] = nooks[id] with { Name = $"nook-{n}" };
                return m with { Nooks = nooks };
            }));
        }
        await Task.WhenAll(tasks);
        await actor.DisposeAsync();

        var final = actor.State;
        Assert.Equal(3, final.Nooks.Count);
        Assert.All(final.Nooks.Values, r => Assert.StartsWith("nook-", r.Name));
    }
}
