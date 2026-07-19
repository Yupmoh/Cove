using System.Linq;
using System.Text.Json;
using Cove.Engine;
using Cove.Engine.Bays;
using Cove.Engine.Layout;
using Cove.Persistence;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BayLayoutCoordinationTests
{
    private static Task<ControlResponse?> Route(BayManager m, LayoutService layout, string uri, JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, layout, m);

    private static JsonElement Create(string name, string dir) =>
        JsonSerializer.SerializeToElement(new BayCreateParams(name, dir), BaysJsonContext.Default.BayCreateParams);

    private static JsonElement Id(string id) =>
        JsonSerializer.SerializeToElement(new BayIdParams(id), BaysJsonContext.Default.BayIdParams);

    [Fact]
    public async Task Create_MakesBayActiveInLayout_AndEmpty()
    {
        int n = 0;
        var layout = new LayoutService();
        await using var mgr = new BayManager(newId: () => $"id-{++n}", layout: layout);

        var created = await Route(mgr, layout, "cove://commands/bay.create", Create("proj", "/tmp/a"));
        var id = created!.Data!.Value.GetProperty("id").GetString()!;

        Assert.Equal(id, layout.ActiveBayId);
        var snap = layout.ToSnapshot(id, "proj", "/tmp/a");
        Assert.Empty(snap.Shores);
    }

    [Fact]
    public async Task Switch_SwapsActiveLayoutBay()
    {
        int n = 0;
        var layout = new LayoutService();
        await using var mgr = new BayManager(newId: () => $"id-{++n}", layout: layout);

        var a = (await Route(mgr, layout, "cove://commands/bay.create", Create("A", "/tmp/a")))!.Data!.Value.GetProperty("id").GetString()!;
        var b = (await Route(mgr, layout, "cove://commands/bay.create", Create("B", "/tmp/b")))!.Data!.Value.GetProperty("id").GetString()!;

        await Route(mgr, layout, "cove://commands/bay.switch", Id(a));
        Assert.Equal(a, layout.ActiveBayId);
        await Route(mgr, layout, "cove://commands/bay.switch", Id(b));
        Assert.Equal(b, layout.ActiveBayId);
    }

    [Fact]
    public async Task LayoutGet_ReflectsActiveBayIdentity()
    {
        int n = 0;
        var layout = new LayoutService();
        await using var mgr = new BayManager(newId: () => $"id-{++n}", layout: layout);

        var a = (await Route(mgr, layout, "cove://commands/bay.create", Create("Alpha", "/tmp/alpha")))!.Data!.Value.GetProperty("id").GetString()!;

        var get = await Route(mgr, layout, "cove://commands/layout.get", null);
        var snap = get!.Data!.Value.Deserialize(Cove.Persistence.CoveJsonContext.Default.BaySnapshot)!;
        Assert.Equal(a, snap.Id);
        Assert.Equal("Alpha", snap.Name);
        Assert.Equal(Path.GetFullPath("/tmp/alpha"), snap.ProjectDir);
    }

    [Fact]
    public async Task Delete_RemovesLayoutBay()
    {
        int n = 0;
        var layout = new LayoutService();
        await using var mgr = new BayManager(newId: () => $"id-{++n}", layout: layout);

        var a = (await Route(mgr, layout, "cove://commands/bay.create", Create("A", "/tmp/a")))!.Data!.Value.GetProperty("id").GetString()!;
        layout.CreateShore("Terminal 1", new NookLeaf { NookId = "p1", Subtabs = new[] { new Subtab("p1", NookType.Terminal) } });

        await Route(mgr, layout, "cove://commands/bay.delete", Id(a));
        Assert.DoesNotContain(a, layout.BayIds);
    }
}
