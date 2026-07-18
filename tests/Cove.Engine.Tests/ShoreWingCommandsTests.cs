using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Engine.Bays;
using Cove.Engine.Layout;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ShoreWingCommandsTests
{
    private static Task<ControlResponse?> Route(BayManager m, LayoutService layout, string uri, JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, layout, m);

    private static JsonElement El<T>(T v, JsonTypeInfo<T> ti) => JsonSerializer.SerializeToElement(v, ti);

    private static async Task<(BayManager Mgr, LayoutService Layout, string BayId)> NewBayAsync()
    {
        int n = 0;
        var mgr = new BayManager(newId: () => $"id-{++n}");
        var ws = await mgr.CreateBayAsync("proj", "/tmp/proj");
        var layout = new LayoutService();
        layout.SetActiveBay(ws.Id);
        return (mgr, layout, ws.Id);
    }

    [Fact]
    public async Task Shore_Create_List_Rename_MoveWing_Close()
    {
        var (mgr, layout, wsId) = await NewBayAsync();
        await using var _ = mgr;

        var created = await Route(mgr, layout, "cove://commands/shore.create",
            El(new ShoreCreateParams(wsId, null, "build"), ShoreWingJsonContext.Default.ShoreCreateParams));
        Assert.True(created!.Ok);
        var shoreId = created.Data!.Value.GetProperty("shoreId").GetString()!;

        var listed = await Route(mgr, layout, "cove://commands/shore.list",
            El(new BayRef(wsId), ShoreWingJsonContext.Default.BayRef));
        Assert.Equal(1, listed!.Data!.Value.GetProperty("shores").GetArrayLength());

        await Route(mgr, layout, "cove://commands/shore.rename",
            El(new ShoreRenameParams(wsId, shoreId, "renamed"), ShoreWingJsonContext.Default.ShoreRenameParams));

        var wing = await Route(mgr, layout, "cove://commands/wing.create",
            El(new WingCreateParams(wsId, "side"), ShoreWingJsonContext.Default.WingCreateParams));
        var wingId = wing!.Data!.Value.GetProperty("wingId").GetString()!;
        await Route(mgr, layout, "cove://commands/shore.move-to-wing",
            El(new ShoreMoveParams(wsId, shoreId, wingId), ShoreWingJsonContext.Default.ShoreMoveParams));

        var relisted = await Route(mgr, layout, "cove://commands/shore.list",
            El(new BayRef(wsId), ShoreWingJsonContext.Default.BayRef));
        var shore = relisted!.Data!.Value.GetProperty("shores").EnumerateArray()
            .Single(e => e.GetProperty("id").GetString() == shoreId);
        Assert.Equal("renamed", shore.GetProperty("name").GetString());
        Assert.Equal(wingId, shore.GetProperty("wingId").GetString());

        var closed = await Route(mgr, layout, "cove://commands/shore.close",
            El(new ShoreTargetParams(wsId, shoreId), ShoreWingJsonContext.Default.ShoreTargetParams));
        Assert.True(closed!.Ok);
        var afterClose = await Route(mgr, layout, "cove://commands/shore.list",
            El(new BayRef(wsId), ShoreWingJsonContext.Default.BayRef));
        Assert.Equal(0, afterClose!.Data!.Value.GetProperty("shores").GetArrayLength());
    }

    [Fact]
    public async Task Shore_Pin_ReflectedInList()
    {
        var (mgr, layout, wsId) = await NewBayAsync();
        await using var _ = mgr;

        var created = await Route(mgr, layout, "cove://commands/shore.create",
            El(new ShoreCreateParams(wsId, null, "s"), ShoreWingJsonContext.Default.ShoreCreateParams));
        var shoreId = created!.Data!.Value.GetProperty("shoreId").GetString()!;

        await Route(mgr, layout, "cove://commands/shore.pin",
            El(new ShorePinParams(wsId, shoreId, true), ShoreWingJsonContext.Default.ShorePinParams));

        var listed = await Route(mgr, layout, "cove://commands/shore.list",
            El(new BayRef(wsId), ShoreWingJsonContext.Default.BayRef));
        var shore = listed!.Data!.Value.GetProperty("shores").EnumerateArray().Single();
        Assert.True(shore.GetProperty("pinned").GetBoolean());
    }

    [Fact]
    public async Task Wing_Create_List_Remove_RehomesShores()
    {
        var (mgr, layout, wsId) = await NewBayAsync();
        await using var _ = mgr;

        var created = await Route(mgr, layout, "cove://commands/shore.create",
            El(new ShoreCreateParams(wsId, null, "s"), ShoreWingJsonContext.Default.ShoreCreateParams));
        var shoreId = created!.Data!.Value.GetProperty("shoreId").GetString()!;

        var wing = await Route(mgr, layout, "cove://commands/wing.create",
            El(new WingCreateParams(wsId, "side"), ShoreWingJsonContext.Default.WingCreateParams));
        var wingId = wing!.Data!.Value.GetProperty("wingId").GetString()!;
        await Route(mgr, layout, "cove://commands/shore.move-to-wing",
            El(new ShoreMoveParams(wsId, shoreId, wingId), ShoreWingJsonContext.Default.ShoreMoveParams));

        var listed = await Route(mgr, layout, "cove://commands/wing.list",
            El(new BayRef(wsId), ShoreWingJsonContext.Default.BayRef));
        Assert.Equal(2, listed!.Data!.Value.GetProperty("wings").GetArrayLength());

        var removed = await Route(mgr, layout, "cove://commands/wing.remove",
            El(new WingTargetParams(wsId, wingId), ShoreWingJsonContext.Default.WingTargetParams));
        Assert.True(removed!.Ok);

        var wingsAfter = await Route(mgr, layout, "cove://commands/wing.list",
            El(new BayRef(wsId), ShoreWingJsonContext.Default.BayRef));
        Assert.Equal(1, wingsAfter!.Data!.Value.GetProperty("wings").GetArrayLength());
        var shoreAfter = (await Route(mgr, layout, "cove://commands/shore.list",
            El(new BayRef(wsId), ShoreWingJsonContext.Default.BayRef)))!.Data!.Value
            .GetProperty("shores").EnumerateArray().Single();
        Assert.Equal(LayoutService.MainWingId, shoreAfter.GetProperty("wingId").GetString());
    }

    [Fact]
    public async Task Wing_Reorder_And_SetIcon_RoundTrip()
    {
        var (mgr, layout, wsId) = await NewBayAsync();
        await using var _ = mgr;

        var w1 = await Route(mgr, layout, "cove://commands/wing.create",
            El(new WingCreateParams(wsId, "alpha"), ShoreWingJsonContext.Default.WingCreateParams));
        var w1Id = w1!.Data!.Value.GetProperty("wingId").GetString()!;
        var w2 = await Route(mgr, layout, "cove://commands/wing.create",
            El(new WingCreateParams(wsId, "beta"), ShoreWingJsonContext.Default.WingCreateParams));
        var w2Id = w2!.Data!.Value.GetProperty("wingId").GetString()!;

        var setIcon = await Route(mgr, layout, "cove://commands/wing.set-icon",
            El(new WingIconParams(wsId, w1Id, "emoji", "🦑"), ShoreWingJsonContext.Default.WingIconParams));
        Assert.True(setIcon!.Ok);

        await Route(mgr, layout, "cove://commands/wing.reorder",
            El(new WingReorderParams(wsId, new[] { w2Id, w1Id, LayoutService.MainWingId }), ShoreWingJsonContext.Default.WingReorderParams));

        var listed = await Route(mgr, layout, "cove://commands/wing.list",
            El(new BayRef(wsId), ShoreWingJsonContext.Default.BayRef));
        var listArr = listed!.Data!.Value.GetProperty("wings").EnumerateArray().ToArray();
        Assert.Equal(w2Id, listArr[0].GetProperty("id").GetString());
        Assert.Equal(w1Id, listArr[1].GetProperty("id").GetString());
        var listedAlpha = listArr.First(e => e.GetProperty("id").GetString() == w1Id);
        Assert.Equal("🦑", listedAlpha.GetProperty("icon").GetProperty("value").GetString());
    }

    [Fact]
    public async Task Commands_UnknownBay_ReturnNotFound()
    {
        await using var mgr = new BayManager();
        var layout = new LayoutService();
        var r = await Route(mgr, layout, "cove://commands/shore.list",
            El(new BayRef("nope"), ShoreWingJsonContext.Default.BayRef));
        Assert.False(r!.Ok);
        Assert.Equal("not_found", r.Error!.Code);
    }
}
