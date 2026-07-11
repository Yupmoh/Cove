using Cove.Engine.Layout;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BrowserNookLayoutTests
{
    [Fact]
    public void CreateBrowserShore_HasBrowserNookType()
    {
        var layout = new LayoutService();
        var leaf = new NookLeaf
        {
            NookId = "bp1",
            Subtabs = new[] { new Subtab("bp1", NookType.Browser) },
        };
        var shoreId = layout.CreateShore("Browser", leaf);
        var snap = layout.ToSnapshot("default", "default", "/tmp");
        var shore = Assert.Single(snap.Shores);
        var leafNode = Assert.IsType<NookLeaf>(shore.LayoutTree);
        var sub = Assert.Single(leafNode.Subtabs);
        Assert.Equal(NookType.Browser, sub.NookType);
    }

    [Fact]
    public void SplitBrowserNook_CreatesBrowserLeaf()
    {
        var layout = new LayoutService();
        var leaf1 = new NookLeaf
        {
            NookId = "bp1",
            Subtabs = new[] { new Subtab("bp1", NookType.Browser) },
        };
        var shoreId = layout.CreateShore("Browser", leaf1);
        var leaf2 = new NookLeaf
        {
            NookId = "bp2",
            Subtabs = new[] { new Subtab("bp2", NookType.Browser) },
        };
        layout.SplitNook(shoreId, "bp1", SplitOrientation.Row, leaf2);
        var snap = layout.ToSnapshot("default", "default", "/tmp");
        var shore = Assert.Single(snap.Shores);
        var split = Assert.IsType<SplitNode>(shore.LayoutTree);
        Assert.Equal(SplitOrientation.Row, split.Orientation);
    }

    [Fact]
    public void NookTypeConverter_RoundTripsBrowser()
    {
        var json = "{\"documentId\":\"bp1\",\"nookType\":\"browser\",\"title\":null}";
        var sub = System.Text.Json.JsonSerializer.Deserialize(json, CoveJsonContext.Default.Subtab)!;
        Assert.Equal(NookType.Browser, sub.NookType);
        var roundTripped = System.Text.Json.JsonSerializer.Serialize(sub, CoveJsonContext.Default.Subtab);
        Assert.Contains("\"browser\"", roundTripped);
    }
}
