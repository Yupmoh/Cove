using Cove.Engine.Knowledge;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class MemoryConsolidatorTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-consol-" + System.Guid.NewGuid().ToString("N"));

    private static (string dir, MemoryStore store, ProposalStore proposals, MemoryConsolidator consolidator) NewStack()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var kernel = new KnowledgePersistenceKernel(dir, NullLogger.Instance);
        kernel.EnsureAllSchemas();
        var store = new MemoryStore(dir, NullLogger.Instance);
        var proposals = new ProposalStore(dir, NullLogger.Instance);
        var consolidator = new MemoryConsolidator(store, proposals, NullLogger.Instance);
        return (dir, store, proposals, consolidator);
    }

    [Fact]
    public async Task ConsolidateAsync_DryRun_DoesNotCreateProposals()
    {
        var (_, store, proposals, consolidator) = NewStack();
        store.AddFact(new Fact { BayId = "ws1", Kind = "decision", Content = "Use SQLite", Confidence = 0.8 });
        store.AddFact(new Fact { BayId = "ws1", Kind = "decision", Content = "Use SQLite", Confidence = 0.5 });

        var count = await consolidator.ConsolidateAsync("ws1", dryRun: true);

        Assert.Equal(1, count);
        Assert.Empty(proposals.ListByBay("ws1"));
    }

    [Fact]
    public async Task ConsolidateAsync_NotDryRun_CreatesProposals()
    {
        var (_, store, proposals, consolidator) = NewStack();
        store.AddFact(new Fact { BayId = "ws1", Kind = "decision", Content = "Use SQLite", Confidence = 0.8 });
        store.AddFact(new Fact { BayId = "ws1", Kind = "decision", Content = "Use SQLite", Confidence = 0.5 });

        var count = await consolidator.ConsolidateAsync("ws1", dryRun: false);

        Assert.Equal(1, count);
        var list = proposals.ListByBay("ws1");
        Assert.Single(list);
        Assert.Equal("proposed", list[0].State);
    }

    [Fact]
    public async Task ConsolidateAsync_IsCancellable()
    {
        var (_, store, _, consolidator) = NewStack();
        store.AddFact(new Fact { BayId = "ws1", Kind = "decision", Content = "fact", Confidence = 0.5 });

        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<System.OperationCanceledException>(async () => await consolidator.ConsolidateAsync("ws1", dryRun: true, cts.Token));
    }

    [Fact]
    public void Proposal_LifecycleAddApplyRevert()
    {
        var (_, _, proposals, _) = NewStack();
        var proposal = proposals.Create("ws1", "merge", "Merge duplicate facts");
        Assert.Equal("proposed", proposal.State);

        Assert.True(proposals.Transition(proposal.Id, "applied"));
        Assert.Equal("applied", proposals.Get(proposal.Id)!.State);

        Assert.True(proposals.Transition(proposal.Id, "reverted"));
        Assert.Equal("reverted", proposals.Get(proposal.Id)!.State);
    }

    [Fact]
    public void Proposal_ListFiltersByState()
    {
        var (_, _, proposals, _) = NewStack();
        var p1 = proposals.Create("ws1", "merge", "A");
        var p2 = proposals.Create("ws1", "merge", "B");
        proposals.Transition(p1.Id, "applied");

        var proposed = proposals.ListByBay("ws1", "proposed");
        var applied = proposals.ListByBay("ws1", "applied");
        Assert.Single(proposed);
        Assert.Single(applied);
        Assert.Equal(p2.Id, proposed[0].Id);
        Assert.Equal(p1.Id, applied[0].Id);
    }

    [Fact]
    public void Proposal_TransitionSameState_ReturnsFalse()
    {
        var (_, _, proposals, _) = NewStack();
        var p = proposals.Create("ws1", "merge", "test");
        Assert.False(proposals.Transition(p.Id, "proposed"));
    }
}
