using System.Text.Json;
using Cove.Engine.Layout;
using Cove.Persistence;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BrowserNookTypeRoundTripTests
{
    private static JsonElement P(LayoutMutateParams p) =>
        JsonSerializer.SerializeToElement(p, Cove.Protocol.CoveJsonContext.Default.LayoutMutateParams);

    private static Task<ControlResponse?> Route(string uri, JsonElement? prm, LayoutService layout) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, layout);

    private static BaySnapshot Snap(LayoutService layout) =>
        layout.ToSnapshot("default", "default", "/tmp");

    [Fact]
    public async Task CreateShore_BrowserNookType_LeafHasBrowserSubtab()
    {
        var layout = new LayoutService();
        var resp = await Route("cove://commands/layout.mutate",
            P(new LayoutMutateParams("createShore", NewNookId: "bp1", Name: "Browser", NookType: "browser")), layout);
        Assert.NotNull(resp);
        Assert.True(resp!.Ok);
        var shore = Assert.Single(Snap(layout).Shores);
        var leaf = Assert.IsType<NookLeaf>(shore.LayoutTree);
        var sub = Assert.Single(leaf.Subtabs);
        Assert.Equal(NookType.Browser, sub.NookType);
    }

    [Fact]
    public async Task CreateShore_DefaultNookType_IsTerminal()
    {
        var layout = new LayoutService();
        var resp = await Route("cove://commands/layout.mutate",
            P(new LayoutMutateParams("createShore", NewNookId: "t1", Name: "Terminal")), layout);
        Assert.NotNull(resp);
        Assert.True(resp!.Ok);
        var shore = Assert.Single(Snap(layout).Shores);
        var leaf = Assert.IsType<NookLeaf>(shore.LayoutTree);
        var sub = Assert.Single(leaf.Subtabs);
        Assert.Equal(NookType.Terminal, sub.NookType);
    }

    [Fact]
    public async Task CreateShore_BrowserNookType_SerializesAsBrowserString()
    {
        var layout = new LayoutService();
        await Route("cove://commands/layout.mutate",
            P(new LayoutMutateParams("createShore", NewNookId: "bp1", Name: "Browser", NookType: "browser")), layout);
        var json = JsonSerializer.Serialize(Snap(layout), Cove.Persistence.CoveJsonContext.Default.BaySnapshot);
        Assert.Contains("\"nookType\": \"browser\"", json);
    }

    [Fact]
    public async Task Split_BrowserNookType_NewLeafIsBrowser()
    {
        var layout = new LayoutService();
        var create = await Route("cove://commands/layout.mutate",
            P(new LayoutMutateParams("createShore", NewNookId: "bp1", Name: "Browser", NookType: "browser")), layout);
        var shoreId = create!.Data!.Value.Deserialize(Cove.Protocol.CoveJsonContext.Default.LayoutMutateResult)!.ShoreId!;
        var resp = await Route("cove://commands/layout.mutate",
            P(new LayoutMutateParams("split", ShoreId: shoreId, TargetNookId: "bp1", NewNookId: "bp2", Orientation: "row", NookType: "browser")), layout);
        Assert.NotNull(resp);
        Assert.True(resp!.Ok);
        var shore = Assert.Single(Snap(layout).Shores);
        var split = Assert.IsType<SplitNode>(shore.LayoutTree);
        var leafA = Assert.IsType<NookLeaf>(split.ChildA);
        var leafB = Assert.IsType<NookLeaf>(split.ChildB);
        Assert.All(new[] { leafA, leafB }, leaf =>
        {
            var sub = Assert.Single(leaf.Subtabs);
            Assert.Equal(NookType.Browser, sub.NookType);
        });
    }
}
