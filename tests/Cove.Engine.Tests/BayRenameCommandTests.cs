using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Engine;
using Cove.Engine.Bays;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BayRenameCommandTests
{
    private static Task<ControlResponse?> Route(BayManager m, string uri, JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, null, m);

    private static JsonElement El<T>(T v, JsonTypeInfo<T> ti) => JsonSerializer.SerializeToElement(v, ti);

    [Fact]
    public async Task Rename_UpdatesNameAndSurfacesInList()
    {
        int n = 0;
        await using var m = new BayManager(newId: () => $"id-{++n}");
        var ws = await m.CreateBayAsync("old", "/a");

        var resp = await Route(m, "cove://commands/bay.rename",
            El(new BayRenameParams(ws.Id, "new"), BayExtraJsonContext.Default.BayRenameParams));

        Assert.True(resp!.Ok);
        Assert.Equal("new", m.Get(ws.Id)!.State.Name);
        Assert.Contains(m.ListBays(), w => w.Id == ws.Id && w.Name == "new");
    }

    [Fact]
    public async Task Rename_EmptyName_IsInvalidParams()
    {
        int n = 0;
        await using var m = new BayManager(newId: () => $"id-{++n}");
        var ws = await m.CreateBayAsync("old", "/a");

        var resp = await Route(m, "cove://commands/bay.rename",
            El(new BayRenameParams(ws.Id, "   "), BayExtraJsonContext.Default.BayRenameParams));

        Assert.False(resp!.Ok);
        Assert.Equal("invalid_params", resp.Error!.Code);
        Assert.Equal("old", m.Get(ws.Id)!.State.Name);
    }

    [Fact]
    public async Task Rename_UnknownBay_IsNotFound()
    {
        await using var m = new BayManager(newId: () => "id-1");

        var resp = await Route(m, "cove://commands/bay.rename",
            El(new BayRenameParams("missing", "new"), BayExtraJsonContext.Default.BayRenameParams));

        Assert.False(resp!.Ok);
        Assert.Equal("not_found", resp.Error!.Code);
    }
}
