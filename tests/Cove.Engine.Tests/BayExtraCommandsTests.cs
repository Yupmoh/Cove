using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Engine;
using Cove.Engine.Bays;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BayExtraCommandsTests
{
    private static Task<ControlResponse?> Route(BayManager m, string uri, JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, null, m);

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
    public async Task MoveShore_MovesBetweenBays()
    {
        int n = 0;
        await using var m = new BayManager(newId: () => $"id-{++n}");
        var a = await m.CreateBayAsync("a", "/a");
        var b = await m.CreateBayAsync("b", "/b");

        var created = await Route(m, "cove://commands/shore.create",
            El(new ShoreCreateParams(a.Id, null, "extra"), ShoreWingJsonContext.Default.ShoreCreateParams));
        var shoreId = created!.Data!.Value.GetProperty("shoreId").GetString()!;

        var moved = await Route(m, "cove://commands/bay.move-shore",
            El(new BayMoveShoreParams(a.Id, shoreId, b.Id), BayExtraJsonContext.Default.BayMoveShoreParams));
        Assert.True(moved!.Ok);
        Assert.DoesNotContain(m.Get(a.Id)!.State.Shores, r => r.Id == shoreId);
        Assert.Contains(m.Get(b.Id)!.State.Shores, r => r.Id == shoreId);
    }
}
