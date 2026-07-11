using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Engine;
using Cove.Engine.Bays;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BayIconListTests
{
    private static Task<ControlResponse?> Route(BayManager m, string uri, JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, null, m);

    private static JsonElement El<T>(T v, JsonTypeInfo<T> ti) => JsonSerializer.SerializeToElement(v, ti);

    private static BaySummary ListItem(ControlResponse resp, string id) =>
        resp.Data!.Value.Deserialize(BaysJsonContext.Default.BayListResult)!
            .Bays.Single(w => w.Id == id);

    [Fact]
    public async Task List_CarriesIcon_AfterSetIcon()
    {
        int n = 0;
        await using var m = new BayManager(newId: () => $"id-{++n}");
        var ws = await m.CreateBayAsync("w", "/a");

        var setResp = await Route(m, "cove://commands/bay.set-icon",
            El(new BayIconParams(ws.Id, "emoji", "🚀"), BayExtraJsonContext.Default.BayIconParams));
        Assert.True(setResp!.Ok);

        var listResp = await Route(m, "cove://commands/bay.list", null);
        var item = ListItem(listResp!, ws.Id);
        Assert.Equal("emoji", item.IconKind);
        Assert.Equal("🚀", item.IconValue);
    }

    [Fact]
    public async Task List_IconNull_WhenNoIcon()
    {
        int n = 0;
        await using var m = new BayManager(newId: () => $"id-{++n}");
        var ws = await m.CreateBayAsync("w", "/a");

        var listResp = await Route(m, "cove://commands/bay.list", null);
        var item = ListItem(listResp!, ws.Id);
        Assert.Null(item.IconKind);
        Assert.Null(item.IconValue);
    }

    [Fact]
    public async Task List_IconCleared_WhenSetIconEmptyKind()
    {
        int n = 0;
        await using var m = new BayManager(newId: () => $"id-{++n}");
        var ws = await m.CreateBayAsync("w", "/a");

        await Route(m, "cove://commands/bay.set-icon",
            El(new BayIconParams(ws.Id, "emoji", "🚀"), BayExtraJsonContext.Default.BayIconParams));
        await Route(m, "cove://commands/bay.set-icon",
            El(new BayIconParams(ws.Id, null, null), BayExtraJsonContext.Default.BayIconParams));

        var listResp = await Route(m, "cove://commands/bay.list", null);
        var item = ListItem(listResp!, ws.Id);
        Assert.Null(item.IconKind);
        Assert.Null(item.IconValue);
    }
}
