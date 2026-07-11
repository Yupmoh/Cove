using Cove.Engine.Layout;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ZoomTests
{
    private static NookLeaf Leaf(string id) => new()
    {
        NookId = id,
        Subtabs = new[] { new Subtab(id, NookType.Terminal) },
    };

    [Fact]
    public void SetZoom_SetsShoreOverlay()
    {
        var layout = new LayoutService();
        string shoreId = layout.CreateShore("main", Leaf("p1"));
        layout.SplitNook(shoreId, "p1", SplitOrientation.Row, Leaf("p2"));
        layout.SetZoom(shoreId, "p1");
        var snap = layout.ToSnapshot("ws", "demo", "/proj");
        Assert.Equal("p1", snap.Shores[0].ZoomedNookId);
    }

    [Fact]
    public void Unzoom_ClearsOverlay()
    {
        var layout = new LayoutService();
        string shoreId = layout.CreateShore("main", Leaf("p1"));
        layout.SetZoom(shoreId, "p1");
        layout.SetZoom(shoreId, null);
        var snap = layout.ToSnapshot("ws", "demo", "/proj");
        Assert.Null(snap.Shores[0].ZoomedNookId);
    }

    [Fact]
    public void Zoom_DoesNotMutateTree()
    {
        var layout = new LayoutService();
        string shoreId = layout.CreateShore("main", Leaf("p1"));
        layout.SplitNook(shoreId, "p1", SplitOrientation.Row, Leaf("p2"));
        var before = layout.GetRoot(shoreId)!;
        layout.SetZoom(shoreId, "p1");
        var after = layout.GetRoot(shoreId)!;
        Assert.Same(before, after);
    }

    [Fact]
    public void Zoom_SurvivesReload()
    {
        var layout = new LayoutService();
        string shoreId = layout.CreateShore("main", Leaf("p1"));
        layout.SplitNook(shoreId, "p1", SplitOrientation.Row, Leaf("p2"));
        layout.SetZoom(shoreId, "p2");
        var snap = layout.ToSnapshot("ws", "demo", "/proj");

        var reloaded = new LayoutService();
        reloaded.LoadSnapshot(snap);
        var reloadedSnap = reloaded.ToSnapshot("ws", "demo", "/proj");
        Assert.Equal("p2", reloadedSnap.Shores[0].ZoomedNookId);
    }
}
