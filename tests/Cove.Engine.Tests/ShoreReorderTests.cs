using Cove.Engine.Layout;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ShoreReorderTests
{
    private static NookLeaf Leaf(string id) => new()
    {
        NookId = id,
        Subtabs = new[] { new Subtab(id, NookType.Terminal) },
    };

    [Fact]
    public void ReorderShores_ChangesSnapshotOrder()
    {
        var svc = new LayoutService();
        var r1 = svc.CreateShore("Shore 1", Leaf("a"));
        var r2 = svc.CreateShore("Shore 2", Leaf("b"));
        var r3 = svc.CreateShore("Shore 3", Leaf("c"));

        svc.ReorderShores(new[] { r3, r1, r2 });

        var snap = svc.ToSnapshot("ws", "demo", "/proj");
        Assert.Equal(r3, snap.Shores[0].Id);
        Assert.Equal(r1, snap.Shores[1].Id);
        Assert.Equal(r2, snap.Shores[2].Id);
    }

    [Fact]
    public void ReorderShores_PreservesAllShores()
    {
        var svc = new LayoutService();
        var r1 = svc.CreateShore("A", Leaf("a"));
        var r2 = svc.CreateShore("B", Leaf("b"));

        svc.ReorderShores(new[] { r2, r1 });

        var snap = svc.ToSnapshot("ws", "demo", "/proj");
        Assert.Equal(2, snap.Shores.Count);
    }

    [Fact]
    public void ReorderShores_IgnoresUnknownIds()
    {
        var svc = new LayoutService();
        var r1 = svc.CreateShore("A", Leaf("a"));
        var r2 = svc.CreateShore("B", Leaf("b"));

        svc.ReorderShores(new[] { r2, r1, "nonexistent" });

        var snap = svc.ToSnapshot("ws", "demo", "/proj");
        Assert.Equal(2, snap.Shores.Count);
        Assert.Equal(r2, snap.Shores[0].Id);
        Assert.Equal(r1, snap.Shores[1].Id);
    }

    [Fact]
    public void ReorderShores_PartialListKeepsRemainingAtEnd()
    {
        var svc = new LayoutService();
        var r1 = svc.CreateShore("A", Leaf("a"));
        var r2 = svc.CreateShore("B", Leaf("b"));
        var r3 = svc.CreateShore("C", Leaf("c"));

        svc.ReorderShores(new[] { r2 });

        var snap = svc.ToSnapshot("ws", "demo", "/proj");
        Assert.Equal(r2, snap.Shores[0].Id);
        Assert.Equal(3, snap.Shores.Count);
    }

    [Fact]
    public void ReorderShores_SurvivesRoundTrip()
    {
        var svc = new LayoutService();
        var r1 = svc.CreateShore("A", Leaf("a"));
        var r2 = svc.CreateShore("B", Leaf("b"));
        var r3 = svc.CreateShore("C", Leaf("c"));
        svc.ReorderShores(new[] { r3, r1, r2 });

        var snap = svc.ToSnapshot("ws", "demo", "/proj");
        var fresh = new LayoutService();
        fresh.LoadSnapshot(snap);
        var snap2 = fresh.ToSnapshot("ws", "demo", "/proj");

        Assert.Equal(r3, snap2.Shores[0].Id);
        Assert.Equal(r1, snap2.Shores[1].Id);
        Assert.Equal(r2, snap2.Shores[2].Id);
    }

    [Fact]
    public void CloseNook_LastNook_KeepsShoreAsEmpty_PreservingOrder()
    {
        var svc = new LayoutService();
        var r1 = svc.CreateShore("A", Leaf("a"));
        var r2 = svc.CreateShore("B", Leaf("b"));
        var r3 = svc.CreateShore("C", Leaf("c"));
        svc.ReorderShores(new[] { r3, r1, r2 });

        svc.CloseNook(r1, "a");

        var snap = svc.ToSnapshot("ws", "demo", "/proj");
        Assert.Equal(3, snap.Shores.Count);
        Assert.Equal(r3, snap.Shores[0].Id);
        Assert.Equal(r1, snap.Shores[1].Id);
        Assert.Equal(r2, snap.Shores[2].Id);
        Assert.True(LayoutService.IsEmptyShore(snap.Shores[1].LayoutTree));
    }

    [Fact]
    public void CloseShore_RemovesShore_PreservingOrder()
    {
        var svc = new LayoutService();
        var r1 = svc.CreateShore("A", Leaf("a"));
        var r2 = svc.CreateShore("B", Leaf("b"));
        var r3 = svc.CreateShore("C", Leaf("c"));
        svc.ReorderShores(new[] { r3, r1, r2 });

        svc.CloseShore(r1);

        var snap = svc.ToSnapshot("ws", "demo", "/proj");
        Assert.Equal(2, snap.Shores.Count);
        Assert.Equal(r3, snap.Shores[0].Id);
        Assert.Equal(r2, snap.Shores[1].Id);
    }
}
