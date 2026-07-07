using Cove.Engine.Layout;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ZoomTests
{
    private static PaneLeaf Leaf(string id) => new()
    {
        PaneId = id,
        Subtabs = new[] { new Subtab(id, PaneType.Terminal) },
    };

    [Fact]
    public void SetZoom_SetsRoomOverlay()
    {
        var layout = new LayoutService();
        string roomId = layout.CreateRoom("main", Leaf("p1"));
        layout.SplitPane(roomId, "p1", SplitOrientation.Row, Leaf("p2"));
        layout.SetZoom(roomId, "p1");
        var snap = layout.ToSnapshot("ws", "demo", "/proj");
        Assert.Equal("p1", snap.Rooms[0].ZoomedPaneId);
    }

    [Fact]
    public void Unzoom_ClearsOverlay()
    {
        var layout = new LayoutService();
        string roomId = layout.CreateRoom("main", Leaf("p1"));
        layout.SetZoom(roomId, "p1");
        layout.SetZoom(roomId, null);
        var snap = layout.ToSnapshot("ws", "demo", "/proj");
        Assert.Null(snap.Rooms[0].ZoomedPaneId);
    }

    [Fact]
    public void Zoom_DoesNotMutateTree()
    {
        var layout = new LayoutService();
        string roomId = layout.CreateRoom("main", Leaf("p1"));
        layout.SplitPane(roomId, "p1", SplitOrientation.Row, Leaf("p2"));
        var before = layout.GetRoot(roomId)!;
        layout.SetZoom(roomId, "p1");
        var after = layout.GetRoot(roomId)!;
        Assert.Same(before, after);
    }

    [Fact]
    public void Zoom_SurvivesReload()
    {
        var layout = new LayoutService();
        string roomId = layout.CreateRoom("main", Leaf("p1"));
        layout.SplitPane(roomId, "p1", SplitOrientation.Row, Leaf("p2"));
        layout.SetZoom(roomId, "p2");
        var snap = layout.ToSnapshot("ws", "demo", "/proj");

        var reloaded = new LayoutService();
        reloaded.LoadSnapshot(snap);
        var reloadedSnap = reloaded.ToSnapshot("ws", "demo", "/proj");
        Assert.Equal("p2", reloadedSnap.Rooms[0].ZoomedPaneId);
    }
}
