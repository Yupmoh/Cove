using Cove.Engine.Layout;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class MovePaneToRoomTests
{
    private static PaneLeaf Leaf(string id) => new PaneLeaf { PaneId = id, Subtabs = new[] { new Subtab(id, PaneType.Terminal) } };

    private static PaneLeaf EmptyLeaf(string id) => new PaneLeaf { PaneId = id, Subtabs = new[] { new Subtab(id, PaneType.Empty) } };

    [Fact]
    public void MovePaneToRoom_AppendsToPopulatedTarget()
    {
        var layout = new LayoutService();
        var r1 = layout.CreateRoom("one", Leaf("a"));
        layout.SplitPane(r1, "a", SplitOrientation.Row, Leaf("b"));
        var r2 = layout.CreateRoom("two", Leaf("c"));

        layout.MovePaneToRoom("a", r2);

        var snap = layout.ToSnapshot("w", "w", "/tmp");
        var room1 = snap.Rooms.Single(r => r.Id == r1);
        var room2 = snap.Rooms.Single(r => r.Id == r2);
        Assert.Equal(new[] { "b" }, MosaicOps.Leaves(room1.LayoutTree).Select(l => l.PaneId));
        Assert.Equal(new[] { "c", "a" }, MosaicOps.Leaves(room2.LayoutTree).Select(l => l.PaneId));
    }

    [Fact]
    public void MovePaneToRoom_ReplacesEmptyTargetRoom()
    {
        var layout = new LayoutService();
        var r1 = layout.CreateRoom("one", Leaf("a"));
        layout.SplitPane(r1, "a", SplitOrientation.Row, Leaf("b"));
        var r2 = layout.CreateRoom("two", EmptyLeaf("empty-1"));

        layout.MovePaneToRoom("a", r2);

        var snap = layout.ToSnapshot("w", "w", "/tmp");
        var room2 = snap.Rooms.Single(r => r.Id == r2);
        Assert.Equal(new[] { "a" }, MosaicOps.Leaves(room2.LayoutTree).Select(l => l.PaneId));
    }

    [Fact]
    public void MovePaneToRoom_LastPaneLeavesSourceRoomEmpty()
    {
        var layout = new LayoutService();
        var r1 = layout.CreateRoom("one", Leaf("a"));
        var r2 = layout.CreateRoom("two", Leaf("b"));

        layout.MovePaneToRoom("a", r2);

        var snap = layout.ToSnapshot("w", "w", "/tmp");
        var room1 = snap.Rooms.Single(r => r.Id == r1);
        var leaves = MosaicOps.Leaves(room1.LayoutTree);
        Assert.Single(leaves);
        Assert.All(leaves[0].Subtabs, s => Assert.Equal(PaneType.Empty, s.PaneType));
    }

    [Fact]
    public void MovePaneToRoom_SameRoom_Throws()
    {
        var layout = new LayoutService();
        var r1 = layout.CreateRoom("one", Leaf("a"));
        Assert.Throws<InvalidOperationException>(() => layout.MovePaneToRoom("a", r1));
    }

    [Fact]
    public void MovePaneToRoom_UnknownPane_Throws()
    {
        var layout = new LayoutService();
        var r1 = layout.CreateRoom("one", Leaf("a"));
        Assert.Throws<KeyNotFoundException>(() => layout.MovePaneToRoom("nope", r1));
    }
}
