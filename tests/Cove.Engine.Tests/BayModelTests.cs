using System.Text.Json;
using Cove.Engine.Bays;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BayModelTests
{
    private static Shore MakeShore(string id, string nookId, string wing = BayModel.MainWingId) =>
        new() { Id = id, Name = "r", WingId = wing, ActiveNookId = nookId, LayoutTree = new NookLeaf { NookId = nookId } };

    private static BayModel Ws(IReadOnlyList<Shore> shores, IReadOnlyList<Wing>? wings = null, string? active = null)
    {
        var nooks = new Dictionary<string, NookRecord>();
        foreach (var r in shores)
            nooks[r.ActiveNookId!] = new NookRecord { NookId = r.ActiveNookId! };
        return new BayModel
        {
            Id = "ws1",
            Name = "proj",
            ProjectDir = "/tmp/proj",
            Wings = wings ?? [new Wing { Id = BayModel.MainWingId, Name = "main" }],
            Shores = shores,
            Nooks = nooks,
            ActiveShoreId = active ?? (shores.Count > 0 ? shores[0].Id : null),
        };
    }

    private static Func<string> Counter()
    {
        int n = 0;
        return () => $"gen-{++n}";
    }

    [Fact]
    public void CloseShore_LastShore_MintsFreshShore()
    {
        var m = Ws([MakeShore("r1", "p1")]);
        var next = BayInvariants.CloseShore(m, "r1", Counter());
        Assert.Single(next.Shores);
        Assert.NotEqual("r1", next.Shores[0].Id);
        Assert.Equal(next.Shores[0].Id, next.ActiveShoreId);
    }

    [Fact]
    public void CloseShore_NonLast_RemovesAndRepointsActive()
    {
        var m = Ws([MakeShore("r1", "p1"), MakeShore("r2", "p2")], active: "r1");
        var next = BayInvariants.CloseShore(m, "r1", Counter());
        Assert.Single(next.Shores);
        Assert.Equal("r2", next.Shores[0].Id);
        Assert.Equal("r2", next.ActiveShoreId);
        Assert.False(next.Nooks.ContainsKey("p1"));
    }

    [Fact]
    public void RemoveWing_RehomesShoresToMain()
    {
        var wings = new Wing[] { new() { Id = "main", Name = "main" }, new() { Id = "w2", Name = "side" } };
        var m = Ws([MakeShore("r1", "p1", "w2")], wings);
        var next = BayInvariants.RemoveWing(m, "w2", Counter());
        Assert.DoesNotContain(next.Wings, w => w.Id == "w2");
        Assert.Equal("main", next.Shores[0].WingId);
    }

    [Fact]
    public void RemoveWing_Main_IsNoOp()
    {
        var m = Ws([MakeShore("r1", "p1")]);
        var next = BayInvariants.RemoveWing(m, "main", Counter());
        Assert.Same(m, next);
    }

    [Fact]
    public void SwitchWing_EmptyWing_MintsShore()
    {
        var wings = new Wing[] { new() { Id = "main", Name = "main" }, new() { Id = "w2", Name = "side" } };
        var m = Ws([MakeShore("r1", "p1", "main")], wings);
        var next = BayInvariants.SwitchWing(m, "w2", Counter());
        Assert.Equal(2, next.Shores.Count);
        var minted = next.Shores.First(r => r.WingId == "w2");
        Assert.Equal(minted.Id, next.ActiveShoreId);
    }

    [Fact]
    public void BayModel_RoundTrips()
    {
        var m = Ws([MakeShore("r1", "p1")]);
        var json = JsonSerializer.Serialize(m, BaysJsonContext.Default.BayModel);
        var back = JsonSerializer.Deserialize(json, BaysJsonContext.Default.BayModel)!;
        Assert.Equal("ws1", back.Id);
        Assert.Equal("proj", back.Name);
        Assert.Single(back.Shores);
        Assert.Equal("r1", back.Shores[0].Id);
        Assert.IsType<NookLeaf>(back.Shores[0].LayoutTree);
        Assert.Equal("p1", ((NookLeaf)back.Shores[0].LayoutTree).NookId);
        Assert.True(back.Nooks.ContainsKey("p1"));
    }

    [Fact]
    public async Task Fuzz_ConcurrentMutations_NoLostUpdates()
    {
        await using var actor = new Actor<BayModel>(Ws([]));
        int n = 0;
        var tasks = Enumerable.Range(0, 50).Select(_ => actor.Mutate(m =>
        {
            var id = $"r-{Interlocked.Increment(ref n)}";
            var shores = new List<Shore>(m.Shores) { MakeShore(id, "p-" + id) };
            return m with { Shores = shores };
        })).ToArray();
        await Task.WhenAll(tasks);
        Assert.Equal(50, actor.State.Shores.Count);
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
        var model = Ws([MakeShore("r1", "p1"), MakeShore("r2", "p2"), MakeShore("r3", "p3")]);
        var actor = new Actor<BayModel>(model);
        var rng = new Random(42);
        var tasks = new List<Task>();
        for (int i = 0; i < 200; i++)
        {
            int n = i;
            tasks.Add(actor.Mutate(m =>
            {
                var rid = m.Shores[n % m.Shores.Count].Id;
                return BayInvariants.RenameShore(m, rid, $"shore-{n}");
            }));
        }
        await Task.WhenAll(tasks);
        await actor.DisposeAsync();

        var final = actor.State;
        Assert.Equal(3, final.Shores.Count);
        Assert.All(final.Shores, r => Assert.StartsWith("shore-", r.Name));
        var names = final.Shores.Select(r => r.Name).ToHashSet();
        Assert.Equal(3, names.Count);
    }
}
