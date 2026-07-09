using System.Text.Json;
using Cove.Engine.Layout;
using Cove.Persistence;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BrowserPaneTypeRoundTripTests
{
    private static JsonElement P(LayoutMutateParams p) =>
        JsonSerializer.SerializeToElement(p, Cove.Protocol.CoveJsonContext.Default.LayoutMutateParams);

    private static Task<ControlResponse?> Route(string uri, JsonElement? prm, LayoutService layout) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, layout);

    private static WorkspaceSnapshot Snap(LayoutService layout) =>
        layout.ToSnapshot("default", "default", "/tmp");

    [Fact]
    public async Task CreateRoom_BrowserPaneType_LeafHasBrowserSubtab()
    {
        var layout = new LayoutService();
        var resp = await Route("cove://commands/layout.mutate",
            P(new LayoutMutateParams("createRoom", NewPaneId: "bp1", Name: "Browser", PaneType: "browser")), layout);
        Assert.NotNull(resp);
        Assert.True(resp!.Ok);
        var room = Assert.Single(Snap(layout).Rooms);
        var leaf = Assert.IsType<PaneLeaf>(room.LayoutTree);
        var sub = Assert.Single(leaf.Subtabs);
        Assert.Equal(PaneType.Browser, sub.PaneType);
    }

    [Fact]
    public async Task CreateRoom_DefaultPaneType_IsTerminal()
    {
        var layout = new LayoutService();
        var resp = await Route("cove://commands/layout.mutate",
            P(new LayoutMutateParams("createRoom", NewPaneId: "t1", Name: "Terminal")), layout);
        Assert.NotNull(resp);
        Assert.True(resp!.Ok);
        var room = Assert.Single(Snap(layout).Rooms);
        var leaf = Assert.IsType<PaneLeaf>(room.LayoutTree);
        var sub = Assert.Single(leaf.Subtabs);
        Assert.Equal(PaneType.Terminal, sub.PaneType);
    }

    [Fact]
    public async Task CreateRoom_BrowserPaneType_SerializesAsBrowserString()
    {
        var layout = new LayoutService();
        await Route("cove://commands/layout.mutate",
            P(new LayoutMutateParams("createRoom", NewPaneId: "bp1", Name: "Browser", PaneType: "browser")), layout);
        var json = JsonSerializer.Serialize(Snap(layout), Cove.Persistence.CoveJsonContext.Default.WorkspaceSnapshot);
        Assert.Contains("\"paneType\": \"browser\"", json);
    }

    [Fact]
    public async Task Split_BrowserPaneType_NewLeafIsBrowser()
    {
        var layout = new LayoutService();
        var create = await Route("cove://commands/layout.mutate",
            P(new LayoutMutateParams("createRoom", NewPaneId: "bp1", Name: "Browser", PaneType: "browser")), layout);
        var roomId = create!.Data!.Value.Deserialize(Cove.Protocol.CoveJsonContext.Default.LayoutMutateResult)!.RoomId!;
        var resp = await Route("cove://commands/layout.mutate",
            P(new LayoutMutateParams("split", RoomId: roomId, TargetPaneId: "bp1", NewPaneId: "bp2", Orientation: "row", PaneType: "browser")), layout);
        Assert.NotNull(resp);
        Assert.True(resp!.Ok);
        var room = Assert.Single(Snap(layout).Rooms);
        var split = Assert.IsType<SplitNode>(room.LayoutTree);
        var leafA = Assert.IsType<PaneLeaf>(split.ChildA);
        var leafB = Assert.IsType<PaneLeaf>(split.ChildB);
        Assert.All(new[] { leafA, leafB }, leaf =>
        {
            var sub = Assert.Single(leaf.Subtabs);
            Assert.Equal(PaneType.Browser, sub.PaneType);
        });
    }
}
