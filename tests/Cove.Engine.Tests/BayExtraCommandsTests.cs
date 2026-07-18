using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Engine;
using Cove.Engine.Bays;
using Cove.Engine.Layout;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BayExtraCommandsTests
{
    private static Task<ControlResponse?> Route(BayManager m, string uri, JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, null, m);

    private static Task<ControlResponse?> RouteL(BayManager m, LayoutService layout, string uri, JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, layout, m);

    private static JsonElement El<T>(T v, JsonTypeInfo<T> ti) => JsonSerializer.SerializeToElement(v, ti);

    [Fact]
    public async Task Hide_Icon_Accent_Apply()
    {
        int n = 0;
        await using var m = new BayManager(newId: () => $"id-{++n}");
        var ws = await m.CreateBayAsync("a", "/a");

        await Route(m, "cove://commands/bay.hide",
            El(new BayHideParams(ws.Id, true), BayExtraJsonContext.Default.BayHideParams));
        await Route(m, "cove://commands/bay.set-accent",
            El(new BayAccentParams(ws.Id, "#ff8800"), BayExtraJsonContext.Default.BayAccentParams));
        await Route(m, "cove://commands/bay.set-icon",
            El(new BayIconParams(ws.Id, "emoji", "rocket"), BayExtraJsonContext.Default.BayIconParams));

        var st = m.Get(ws.Id)!.State;
        Assert.True(st.Hidden);
        Assert.Equal("#ff8800", st.AccentColor);
        Assert.Equal("emoji", st.Icon!.Kind);
        Assert.Equal("rocket", st.Icon.Value);
    }

    [Fact]
    public async Task Reorder_ReordersOpenBays()
    {
        int n = 0;
        await using var m = new BayManager(newId: () => $"id-{++n}");
        var a = await m.CreateBayAsync("a", "/a");
        var b = await m.CreateBayAsync("b", "/b");
        var c = await m.CreateBayAsync("c", "/c");

        await Route(m, "cove://commands/bay.reorder",
            El(new BayReorderParams(new[] { c.Id, a.Id, b.Id }), BayExtraJsonContext.Default.BayReorderParams));
        Assert.Equal(new[] { c.Id, a.Id, b.Id }, m.Registry.OpenBays.ToArray());
    }

    [Fact]
    public async Task Reorder_InvokesOrderPersistence()
    {
        int n = 0;
        System.Collections.Generic.IReadOnlyList<string>? persisted = null;
        await using var m = new BayManager(newId: () => $"id-{++n}", persistOrder: ids => persisted = ids);
        var a = await m.CreateBayAsync("a", "/a");
        var b = await m.CreateBayAsync("b", "/b");

        await m.ReorderBaysAsync(new[] { b.Id, a.Id });
        Assert.Equal(new[] { b.Id, a.Id }, persisted);
    }

    [Fact]
    public async Task MoveShore_MovesBetweenBays()
    {
        int n = 0;
        await using var m = new BayManager(newId: () => $"id-{++n}");
        var a = await m.CreateBayAsync("a", "/a");
        var b = await m.CreateBayAsync("b", "/b");
        var layout = new LayoutService();
        layout.EnsureBay(a.Id);
        layout.EnsureBay(b.Id);

        var created = await RouteL(m, layout, "cove://commands/shore.create",
            El(new ShoreCreateParams(a.Id, null, "extra"), ShoreWingJsonContext.Default.ShoreCreateParams));
        var shoreId = created!.Data!.Value.GetProperty("shoreId").GetString()!;

        var moved = await RouteL(m, layout, "cove://commands/bay.move-shore",
            El(new BayMoveShoreParams(a.Id, shoreId, b.Id), BayExtraJsonContext.Default.BayMoveShoreParams));
        Assert.True(moved!.Ok);

        var aShores = (await RouteL(m, layout, "cove://commands/shore.list",
            El(new BayRef(a.Id), ShoreWingJsonContext.Default.BayRef)))!.Data!.Value.GetProperty("shores");
        var bShores = (await RouteL(m, layout, "cove://commands/shore.list",
            El(new BayRef(b.Id), ShoreWingJsonContext.Default.BayRef)))!.Data!.Value.GetProperty("shores");
        Assert.DoesNotContain(aShores.EnumerateArray(), e => e.GetProperty("id").GetString() == shoreId);
        Assert.Contains(bShores.EnumerateArray(), e => e.GetProperty("id").GetString() == shoreId);
    }
}
