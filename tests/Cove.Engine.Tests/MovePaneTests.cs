using Cove.Engine.Layout;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class MovePaneTests
{
    private static PaneLeaf Leaf(string id, params string[] extraSubtabs)
    {
        var subs = new Subtab[1 + extraSubtabs.Length];
        subs[0] = new Subtab(id, PaneType.Terminal);
        for (var i = 0; i < extraSubtabs.Length; i++)
            subs[i + 1] = new Subtab(extraSubtabs[i], PaneType.Terminal);
        return new PaneLeaf { PaneId = id, Subtabs = subs };
    }

    private static (LayoutService Layout, string RoomId) RoomWithThree()
    {
        var layout = new LayoutService();
        var roomId = layout.CreateRoom("room", Leaf("a"));
        layout.SplitPane(roomId, "a", SplitOrientation.Row, Leaf("b"));
        layout.SplitPane(roomId, "b", SplitOrientation.Row, Leaf("c"));
        return (layout, roomId);
    }

    private static string[] LeafOrder(LayoutService layout, string roomId)
    {
        var room = layout.ToSnapshot("w", "w", "/tmp").Rooms.Single(r => r.Id == roomId);
        return MosaicOps.Leaves(room.LayoutTree).Select(l => l.PaneId).ToArray();
    }

    [Fact]
    public void MovePane_AfterTarget_ReordersLeaves()
    {
        var (layout, roomId) = RoomWithThree();
        layout.MovePane(roomId, "a", "c", SplitOrientation.Row, 1);
        Assert.Equal(new[] { "b", "c", "a" }, LeafOrder(layout, roomId));
    }

    [Fact]
    public void MovePane_BeforeTarget_InsertsOnNearSide()
    {
        var (layout, roomId) = RoomWithThree();
        layout.MovePane(roomId, "c", "a", SplitOrientation.Row, -1);
        Assert.Equal(new[] { "c", "a", "b" }, LeafOrder(layout, roomId));
    }

    [Fact]
    public void MovePane_PreservesSubtabs()
    {
        var layout = new LayoutService();
        var roomId = layout.CreateRoom("room", Leaf("a", "a2"));
        layout.SplitPane(roomId, "a", SplitOrientation.Row, Leaf("b"));
        layout.MovePane(roomId, "a", "b", SplitOrientation.Column, 1);
        var room = layout.ToSnapshot("w", "w", "/tmp").Rooms.Single(r => r.Id == roomId);
        var moved = MosaicOps.Leaves(room.LayoutTree).Single(l => l.PaneId == "a");
        Assert.Equal(2, moved.Subtabs.Count);
    }

    [Fact]
    public void MovePane_OntoItself_Throws()
    {
        var (layout, roomId) = RoomWithThree();
        Assert.Throws<InvalidOperationException>(() => layout.MovePane(roomId, "a", "a", SplitOrientation.Row, 1));
    }

    [Fact]
    public void MovePane_OnlyPane_Throws()
    {
        var layout = new LayoutService();
        var roomId = layout.CreateRoom("room", Leaf("a"));
        Assert.Throws<InvalidOperationException>(() => layout.MovePane(roomId, "a", "a", SplitOrientation.Row, 1));
    }
}
