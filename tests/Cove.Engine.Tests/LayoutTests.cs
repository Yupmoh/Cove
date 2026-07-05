using System.Linq;
using System.Text.Json;
using Cove.Engine.Layout;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class LayoutTests
{
    private static PaneLeaf Leaf(string id) => new()
    {
        PaneId = id,
        Subtabs = new[] { new Subtab(id + "-d", PaneType.Terminal) },
    };

    [Fact]
    public void Split_AddsLeaf_AndReplacesTarget()
    {
        var a = Leaf("a");
        var b = Leaf("b");
        var root = MosaicOps.Split(a, "a", SplitOrientation.Row, b);

        var leaves = MosaicOps.Leaves(root);
        Assert.Equal(2, leaves.Count);
        Assert.Equal("a", leaves[0].PaneId);
        Assert.Equal("b", leaves[1].PaneId);
        Assert.IsType<SplitNode>(root);
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
        Assert.DoesNotContain(after, l => l.PaneId == "b");
        Assert.True(closed is SplitNode || closed is PaneLeaf);
    }

    [Fact]
    public void Close_RootLeaf_ReturnsNull()
    {
        var a = Leaf("a");
        Assert.Null(MosaicOps.Close(a, "a"));
    }

    [Fact]
    public void NextPane_CyclesInOrderAndWraps()
    {
        var a = Leaf("a");
        var b = Leaf("b");
        var c = Leaf("c");

        var root = MosaicOps.Split(a, "a", SplitOrientation.Row, b);
        root = MosaicOps.Split(root, "b", SplitOrientation.Column, c);

        Assert.Equal("a", MosaicOps.NextPane(root, "c", 1));
        Assert.Equal("c", MosaicOps.NextPane(root, "a", -1));
    }

    [Fact]
    public void LayoutService_SplitCloseFocus_KeepsValidState()
    {
        var svc = new LayoutService();
        var roomId = svc.CreateRoom("room", Leaf("a"));
        svc.SplitPane(roomId, "a", SplitOrientation.Row, Leaf("b"));
        svc.SplitPane(roomId, "b", SplitOrientation.Column, Leaf("c"));

        Assert.Equal("c", svc.GetActive(roomId));

        svc.ClosePane(roomId, "c");

        var active = svc.GetActive(roomId);
        Assert.NotNull(active);
        var leaves = MosaicOps.Leaves(svc.GetRoot(roomId)!);
        Assert.Contains(leaves, l => l.PaneId == active);
    }

    [Fact]
    public void Snapshot_RoundTrips()
    {
        var svc = new LayoutService();
        var roomId = svc.CreateRoom("room", Leaf("a"));
        svc.SplitPane(roomId, "a", SplitOrientation.Row, Leaf("b"));

        var snap = svc.ToSnapshot("ws", "workspace", "/proj");

        var fresh = new LayoutService();
        fresh.LoadSnapshot(snap);
        var snap2 = fresh.ToSnapshot("ws", "workspace", "/proj");

        var s1 = JsonSerializer.Serialize(snap, CoveJsonContext.Default.WorkspaceSnapshot);
        var s2 = JsonSerializer.Serialize(snap2, CoveJsonContext.Default.WorkspaceSnapshot);
        Assert.Equal(s1, s2);
    }
}
