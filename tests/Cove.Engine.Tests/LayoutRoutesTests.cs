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

    private static string? ShoreIdOf(ControlResponse r)
    {
        Assert.True(r.Ok);
        Assert.NotNull(r.Data);
        var res = r.Data!.Value.Deserialize(Cove.Protocol.CoveJsonContext.Default.LayoutMutateResult);
        Assert.NotNull(res);
        return res!.ShoreId;
    }

    private static int LeafCount(ControlResponse r)
    {
        Assert.True(r.Ok);
        Assert.NotNull(r.Data);
        var snap = r.Data!.Value.Deserialize(Cove.Persistence.CoveJsonContext.Default.BaySnapshot);
        Assert.NotNull(snap);
        var shore = snap!.Shores.FirstOrDefault();
        Assert.NotNull(shore);
        return MosaicOps.Leaves(shore!.LayoutTree).Count;
    }

    [Fact]
    public async Task Get_WithBayId_ReturnsThatBaySnapshot()
    {
        var layout = new LayoutService();
        layout.SetActiveBay("ws-a");
        await Route("cove://commands/layout.mutate", P(new LayoutMutateParams("createShore", NewNookId: "pa", Name: "alpha")), layout);
        layout.SetActiveBay("ws-b");
        await Route("cove://commands/layout.mutate", P(new LayoutMutateParams("createShore", NewNookId: "pb", Name: "beta")), layout);

        var prm = JsonSerializer.SerializeToElement(new LayoutGetParams("ws-a"), Cove.Protocol.CoveJsonContext.Default.LayoutGetParams);
        var forA = await Route("cove://commands/layout.get", prm, layout);
        Assert.True(forA!.Ok);
        var snapA = forA.Data!.Value.Deserialize(Cove.Persistence.CoveJsonContext.Default.BaySnapshot);
        Assert.Equal("ws-a", snapA!.Id);
        Assert.Equal("alpha", snapA.Shores.Single().Name);

        var noParams = await Route("cove://commands/layout.get", null, layout);
        var snapActive = noParams!.Data!.Value.Deserialize(Cove.Persistence.CoveJsonContext.Default.BaySnapshot);
        Assert.Equal("ws-b", snapActive!.Id);
    }

    [Fact]
    public async Task Mutate_CreateSplitClose_DrivesLayout()
    {
        var layout = new LayoutService();

        var create = await Route(
            "cove://commands/layout.mutate",
            P(new LayoutMutateParams("createShore", NewNookId: "p1", Name: "main")),
            layout);
        Assert.NotNull(create);
        var shoreId = ShoreIdOf(create!);
        Assert.NotNull(shoreId);

        var split = await Route(
            "cove://commands/layout.mutate",
            P(new LayoutMutateParams("split", ShoreId: shoreId, TargetNookId: "p1", NewNookId: "p2", Orientation: "row")),
            layout);
        Assert.NotNull(split);
        Assert.True(split!.Ok);

        var get = await Route("cove://commands/layout.get", null, layout);
        Assert.NotNull(get);
        Assert.Equal(2, LeafCount(get!));

        var close = await Route(
            "cove://commands/layout.mutate",
            P(new LayoutMutateParams("close", ShoreId: shoreId, NookId: "p2")),
            layout);
        Assert.NotNull(close);
        Assert.True(close!.Ok);

        var get2 = await Route("cove://commands/layout.get", null, layout);
        Assert.NotNull(get2);
        Assert.Equal(1, LeafCount(get2!));
    }

    [Fact]
    public async Task Mutate_UnknownShore_Fails()
    {
        var layout = new LayoutService();

        var resp = await Route(
            "cove://commands/layout.mutate",
            P(new LayoutMutateParams("split", ShoreId: "does-not-exist", TargetNookId: "p1", NewNookId: "p2")),
            layout);

        Assert.NotNull(resp);
        Assert.False(resp!.Ok);
        Assert.NotNull(resp.Error);
        Assert.Equal("not_found", resp.Error!.Code);
    }
}
