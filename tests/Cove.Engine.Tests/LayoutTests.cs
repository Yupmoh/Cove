using System.Linq;
using System.Text.Json;
using Cove.Engine.Layout;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class LayoutTests
{
    private static NookLeaf Leaf(string id) => new()
    {
        NookId = id,
        Subtabs = new[] { new Subtab(id + "-d", NookType.Terminal) },
    };

    [Fact]
    public void Split_AddsLeaf_AndReplacesTarget()
    {
        var a = Leaf("a");
        var b = Leaf("b");
        var root = MosaicOps.Split(a, "a", SplitOrientation.Row, b);

        var leaves = MosaicOps.Leaves(root);
        Assert.Equal(2, leaves.Count);
        Assert.Equal("a", leaves[0].NookId);
        Assert.Equal("b", leaves[1].NookId);
        Assert.IsType<SplitNode>(root);
    }

    [Fact]
    public void SplitNook_BeforePlacesNewLeafFirst()
    {
        var svc = new LayoutService();
        var shoreId = svc.CreateShore("shore", Leaf("a"));

        svc.SplitNook(
            shoreId,
            "a",
            SplitOrientation.Column,
            Leaf("b"),
            before: true);

        var split = Assert.IsType<SplitNode>(svc.GetRoot(shoreId));
        Assert.Equal("b", Assert.IsType<NookLeaf>(split.ChildA).NookId);
        Assert.Equal("a", Assert.IsType<NookLeaf>(split.ChildB).NookId);
    }

    [Fact]
    public void Close_Reflows_SiblingReplacesParent()
    {
        var a = Leaf("a");
        var b = Leaf("b");
        var c = Leaf("c");

        var root = MosaicOps.Split(a, "a", SplitOrientation.Row, b);
        root = MosaicOps.Split(root, "b", SplitOrientation.Column, c);

        var before = MosaicOps.Leaves(root);
        Assert.Equal(3, before.Count);

        var closed = MosaicOps.Close(root, "b");
        Assert.NotNull(closed);

        var after = MosaicOps.Leaves(closed!);
        Assert.Equal(2, after.Count);
        Assert.DoesNotContain(after, l => l.NookId == "b");
        Assert.True(closed is SplitNode || closed is NookLeaf);
    }

    [Fact]
    public void Close_RootLeaf_ReturnsNull()
    {
        var a = Leaf("a");
        Assert.Null(MosaicOps.Close(a, "a"));
    }

    [Fact]
    public void NextNook_CyclesInOrderAndWraps()
    {
        var a = Leaf("a");
        var b = Leaf("b");
        var c = Leaf("c");

        var root = MosaicOps.Split(a, "a", SplitOrientation.Row, b);
        root = MosaicOps.Split(root, "b", SplitOrientation.Column, c);

        Assert.Equal("a", MosaicOps.NextNook(root, "c", 1));
        Assert.Equal("c", MosaicOps.NextNook(root, "a", -1));
    }

    [Fact]
    public void LayoutService_SplitCloseFocus_KeepsValidState()
    {
        var svc = new LayoutService();
        var shoreId = svc.CreateShore("shore", Leaf("a"));
        svc.SplitNook(shoreId, "a", SplitOrientation.Row, Leaf("b"));
        svc.SplitNook(shoreId, "b", SplitOrientation.Column, Leaf("c"));

        Assert.Equal("c", svc.GetActive(shoreId));

        svc.CloseNook(shoreId, "c");

        var active = svc.GetActive(shoreId);
        Assert.NotNull(active);
        var leaves = MosaicOps.Leaves(svc.GetRoot(shoreId)!);
        Assert.Contains(leaves, l => l.NookId == active);
    }

    [Fact]
    public void Snapshot_RoundTrips()
    {
        var svc = new LayoutService();
        var shoreId = svc.CreateShore("shore", Leaf("a"));
        svc.SplitNook(shoreId, "a", SplitOrientation.Row, Leaf("b"));

        var snap = svc.ToSnapshot("ws", "bay", "/proj");

        var fresh = new LayoutService();
        fresh.LoadSnapshot(snap);
        var snap2 = fresh.ToSnapshot("ws", "bay", "/proj");

        var s1 = JsonSerializer.Serialize(snap, CoveJsonContext.Default.BaySnapshot);
        var s2 = JsonSerializer.Serialize(snap2, CoveJsonContext.Default.BaySnapshot);
        Assert.Equal(s1, s2);
    }
}
