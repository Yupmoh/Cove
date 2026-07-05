using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cove.Engine.Layout;
using Cove.Persistence;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class LayoutRoutesTests
{
    private static JsonElement P(LayoutMutateParams p) =>
        JsonSerializer.SerializeToElement(p, Cove.Protocol.CoveJsonContext.Default.LayoutMutateParams);

    private static Task<ControlResponse?> Route(string uri, JsonElement? prm, LayoutService layout) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, layout);

    private static string? RoomIdOf(ControlResponse r)
    {
        Assert.True(r.Ok);
        Assert.NotNull(r.Data);
        var res = r.Data!.Value.Deserialize(Cove.Protocol.CoveJsonContext.Default.LayoutMutateResult);
        Assert.NotNull(res);
        return res!.RoomId;
    }

    private static int LeafCount(ControlResponse r)
    {
        Assert.True(r.Ok);
        Assert.NotNull(r.Data);
        var snap = r.Data!.Value.Deserialize(Cove.Persistence.CoveJsonContext.Default.WorkspaceSnapshot);
        Assert.NotNull(snap);
        var room = snap!.Rooms.FirstOrDefault();
        Assert.NotNull(room);
        return MosaicOps.Leaves(room!.LayoutTree).Count;
    }

    [Fact]
    public async Task Mutate_CreateSplitClose_DrivesLayout()
    {
        var layout = new LayoutService();

        var create = await Route(
            "cove://commands/layout.mutate",
            P(new LayoutMutateParams("createRoom", NewPaneId: "p1", Name: "main")),
            layout);
        Assert.NotNull(create);
        var roomId = RoomIdOf(create!);
        Assert.NotNull(roomId);

        var split = await Route(
            "cove://commands/layout.mutate",
            P(new LayoutMutateParams("split", RoomId: roomId, TargetPaneId: "p1", NewPaneId: "p2", Orientation: "row")),
            layout);
        Assert.NotNull(split);
        Assert.True(split!.Ok);

        var get = await Route("cove://commands/layout.get", null, layout);
        Assert.NotNull(get);
        Assert.Equal(2, LeafCount(get!));

        var close = await Route(
            "cove://commands/layout.mutate",
            P(new LayoutMutateParams("close", RoomId: roomId, PaneId: "p2")),
            layout);
        Assert.NotNull(close);
        Assert.True(close!.Ok);

        var get2 = await Route("cove://commands/layout.get", null, layout);
        Assert.NotNull(get2);
        Assert.Equal(1, LeafCount(get2!));
    }

    [Fact]
    public async Task Mutate_UnknownRoom_Fails()
    {
        var layout = new LayoutService();

        var resp = await Route(
            "cove://commands/layout.mutate",
            P(new LayoutMutateParams("split", RoomId: "does-not-exist", TargetPaneId: "p1", NewPaneId: "p2")),
            layout);

        Assert.NotNull(resp);
        Assert.False(resp!.Ok);
        Assert.NotNull(resp.Error);
        Assert.Equal("not_found", resp.Error!.Code);
    }
}
