using Cove.Engine.Layout;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BrowserPaneLayoutTests
{
    [Fact]
    public void CreateBrowserRoom_HasBrowserPaneType()
    {
        var layout = new LayoutService();
        var leaf = new PaneLeaf
        {
            PaneId = "bp1",
            Subtabs = new[] { new Subtab("bp1", PaneType.Browser) },
        };
        var roomId = layout.CreateRoom("Browser", leaf);
        var snap = layout.ToSnapshot("default", "default", "/tmp");
        var room = Assert.Single(snap.Rooms);
        var leafNode = Assert.IsType<PaneLeaf>(room.LayoutTree);
        var sub = Assert.Single(leafNode.Subtabs);
        Assert.Equal(PaneType.Browser, sub.PaneType);
    }

    [Fact]
    public void SplitBrowserPane_CreatesBrowserLeaf()
    {
        var layout = new LayoutService();
        var leaf1 = new PaneLeaf
        {
            PaneId = "bp1",
            Subtabs = new[] { new Subtab("bp1", PaneType.Browser) },
        };
        var roomId = layout.CreateRoom("Browser", leaf1);
        var leaf2 = new PaneLeaf
        {
            PaneId = "bp2",
            Subtabs = new[] { new Subtab("bp2", PaneType.Browser) },
        };
        layout.SplitPane(roomId, "bp1", SplitOrientation.Row, leaf2);
        var snap = layout.ToSnapshot("default", "default", "/tmp");
        var room = Assert.Single(snap.Rooms);
        var split = Assert.IsType<SplitNode>(room.LayoutTree);
        Assert.Equal(SplitOrientation.Row, split.Orientation);
    }

    [Fact]
    public void PaneTypeConverter_RoundTripsBrowser()
    {
        var json = "{\"documentId\":\"bp1\",\"paneType\":\"browser\",\"title\":null}";
        var sub = System.Text.Json.JsonSerializer.Deserialize(json, CoveJsonContext.Default.Subtab)!;
        Assert.Equal(PaneType.Browser, sub.PaneType);
        var roundTripped = System.Text.Json.JsonSerializer.Serialize(sub, CoveJsonContext.Default.Subtab);
        Assert.Contains("\"browser\"", roundTripped);
    }
}
