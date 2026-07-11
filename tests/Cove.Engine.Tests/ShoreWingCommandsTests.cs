using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Engine;
using Cove.Engine.Bays;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ShoreWingCommandsTests
{
    private static Task<ControlResponse?> Route(BayManager m, string uri, JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, null, m);

    private static JsonElement El<T>(T v, JsonTypeInfo<T> ti) => JsonSerializer.SerializeToElement(v, ti);

    [Fact]
    public async Task Shore_Create_List_Rename_MoveWing_Close()
    {
        int n = 0;
        await using var mgr = new BayManager(newId: () => $"id-{++n}");
        var ws = await mgr.CreateBayAsync("proj", "/tmp/proj");
        var wsId = ws.Id;

        var created = await Route(mgr, "cove://commands/shore.create",
            El(new ShoreCreateParams(wsId, null, "build"), ShoreWingJsonContext.Default.ShoreCreateParams));
        Assert.True(created!.Ok);
        var shoreId = created.Data!.Value.GetProperty("shoreId").GetString()!;

        var listed = await Route(mgr, "cove://commands/shore.list",
            El(new BayRef(wsId), ShoreWingJsonContext.Default.BayRef));
        Assert.Equal(1, listed!.Data!.Value.GetProperty("shores").GetArrayLength());

        await Route(mgr, "cove://commands/shore.rename",
            El(new ShoreRenameParams(wsId, shoreId, "renamed"), ShoreWingJsonContext.Default.ShoreRenameParams));

        var wing = await Route(mgr, "cove://commands/wing.create",
            El(new WingCreateParams(wsId, "side"), ShoreWingJsonContext.Default.WingCreateParams));
        var wingId = wing!.Data!.Value.GetProperty("wingId").GetString()!;
        await Route(mgr, "cove://commands/shore.move-to-wing",
            El(new ShoreMoveParams(wsId, shoreId, wingId), ShoreWingJsonContext.Default.ShoreMoveParams));

        var shore = mgr.Get(wsId)!.State.Shores.First(r => r.Id == shoreId);
        Assert.Equal("renamed", shore.Name);
        Assert.Equal(wingId, shore.WingId);

        var closed = await Route(mgr, "cove://commands/shore.close",
            El(new ShoreTargetParams(wsId, shoreId), ShoreWingJsonContext.Default.ShoreTargetParams));
        Assert.True(closed!.Ok);
        Assert.Single(mgr.Get(wsId)!.State.Shores);
    }

    [Fact]
    public async Task Wing_Create_List_Remove_RehomesShores()
    {
        int n = 0;
        await using var mgr = new BayManager(newId: () => $"id-{++n}");
        var ws = await mgr.CreateBayAsync("proj", "/tmp/proj");

        var wing = await Route(mgr, "cove://commands/wing.create",
            El(new WingCreateParams(ws.Id, "side"), ShoreWingJsonContext.Default.WingCreateParams));
        var wingId = wing!.Data!.Value.GetProperty("wingId").GetString()!;

        var listed = await Route(mgr, "cove://commands/wing.list",
            El(new BayRef(ws.Id), ShoreWingJsonContext.Default.BayRef));
        Assert.Equal(2, listed!.Data!.Value.GetProperty("wings").GetArrayLength());

        var removed = await Route(mgr, "cove://commands/wing.remove",
            El(new WingTargetParams(ws.Id, wingId), ShoreWingJsonContext.Default.WingTargetParams));
        Assert.True(removed!.Ok);
        Assert.DoesNotContain(mgr.Get(ws.Id)!.State.Wings, w => w.Id == wingId);
    }
    [Fact]
    public async Task Wing_Reorder_And_SetIcon_RoundTrip()
    {
        int n = 0;
        await using var mgr = new BayManager(newId: () => $"id-{++n}");
        var ws = await mgr.CreateBayAsync("proj", "/tmp/proj");
        var wsId = ws.Id;

        var w1 = await Route(mgr, "cove://commands/wing.create",
            El(new WingCreateParams(wsId, "alpha"), ShoreWingJsonContext.Default.WingCreateParams));
        var w1Id = w1!.Data!.Value.GetProperty("wingId").GetString()!;
        var w2 = await Route(mgr, "cove://commands/wing.create",
            El(new WingCreateParams(wsId, "beta"), ShoreWingJsonContext.Default.WingCreateParams));
        var w2Id = w2!.Data!.Value.GetProperty("wingId").GetString()!;

        var setIcon = await Route(mgr, "cove://commands/wing.set-icon",
            El(new WingIconParams(wsId, w1Id, "emoji", "🦑"), ShoreWingJsonContext.Default.WingIconParams));
        Assert.True(setIcon!.Ok);

        var wings = mgr.Get(wsId)!.State.Wings;
        var alpha = wings.First(w => w.Id == w1Id);
        Assert.Equal("🦑", alpha.Icon!.Value);

        await Route(mgr, "cove://commands/wing.reorder",
            El(new WingReorderParams(wsId, new[] { w2Id, w1Id, BayModel.MainWingId }), ShoreWingJsonContext.Default.WingReorderParams));
        var reordered = mgr.Get(wsId)!.State.Wings;
        Assert.Equal(w2Id, reordered[0].Id);
        Assert.Equal(w1Id, reordered[1].Id);

        var listed = await Route(mgr, "cove://commands/wing.list",
            El(new BayRef(wsId), ShoreWingJsonContext.Default.BayRef));
        var listArr = listed!.Data!.Value.GetProperty("wings").EnumerateArray().ToArray();
        var listedAlpha = listArr.First(e => e.GetProperty("id").GetString() == w1Id);
        Assert.Equal("🦑", listedAlpha.GetProperty("icon").GetProperty("value").GetString());
    }
}
