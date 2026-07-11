using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Engine;
using Cove.Engine.Bays;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ResidentCommandsTests
{
    private static Task<ControlResponse?> Route(BayManager m, string uri, JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, null, m);

    private static JsonElement El<T>(T v, JsonTypeInfo<T> ti) => JsonSerializer.SerializeToElement(v, ti);

    private static async Task<List<string?>> ResidentIds(BayManager m, string wsId)
    {
        var listed = await Route(m, "cove://commands/resident.list",
            El(new ResidentListParams(wsId), ResidentJsonContext.Default.ResidentListParams));
        var arr = listed!.Data!.Value.GetProperty("residents");
        return Enumerable.Range(0, arr.GetArrayLength()).Select(i => arr[i].GetProperty("nookId").GetString()).ToList();
    }

    [Fact]
    public async Task Residents_BayScoped_And_Global_Resolution()
    {
        int n = 0;
        await using var m = new BayManager(newId: () => $"id-{++n}");
        var a = await m.CreateBayAsync("a", "/a");
        var b = await m.CreateBayAsync("b", "/b");

        var d1 = await Route(m, "cove://commands/resident.dock",
            El(new ResidentDockParams(a.Id, null, "bay", 0), ResidentJsonContext.Default.ResidentDockParams));
        var wsNook = d1!.Data!.Value.GetProperty("nookId").GetString()!;

        var d2 = await Route(m, "cove://commands/resident.dock",
            El(new ResidentDockParams(b.Id, null, "global", 1), ResidentJsonContext.Default.ResidentDockParams));
        var globalNook = d2!.Data!.Value.GetProperty("nookId").GetString()!;

        var idsA = await ResidentIds(m, a.Id);
        Assert.Contains(wsNook, idsA);
        Assert.Contains(globalNook, idsA);

        var idsB = await ResidentIds(m, b.Id);
        Assert.DoesNotContain(wsNook, idsB);
        Assert.Contains(globalNook, idsB);

        await Route(m, "cove://commands/resident.undock",
            El(new ResidentTargetParams(a.Id, wsNook), ResidentJsonContext.Default.ResidentTargetParams));
        var idsA2 = await ResidentIds(m, a.Id);
        Assert.DoesNotContain(wsNook, idsA2);
        Assert.Contains(globalNook, idsA2);
    }

    [Fact]
    public async Task Resident_SetCollapsed_Persists()
    {
        int n = 0;
        await using var m = new BayManager(newId: () => $"id-{++n}");
        var a = await m.CreateBayAsync("a", "/a");
        var d = await Route(m, "cove://commands/resident.dock",
            El(new ResidentDockParams(a.Id, null, "bay", 0), ResidentJsonContext.Default.ResidentDockParams));
        var nookId = d!.Data!.Value.GetProperty("nookId").GetString()!;

        await Route(m, "cove://commands/resident.set-collapsed",
            El(new ResidentCollapseParams(a.Id, nookId, true), ResidentJsonContext.Default.ResidentCollapseParams));
        Assert.True(m.Get(a.Id)!.State.Nooks[nookId].ResidentCollapsed);
    }
    [Fact]
    public async Task Resident_SetHeight_Persists()
    {
        int n = 0;
        await using var m = new BayManager(newId: () => $"id-{++n}");
        var a = await m.CreateBayAsync("a", "/a");
        var d = await Route(m, "cove://commands/resident.dock",
            El(new ResidentDockParams(a.Id, null, "bay", 0), ResidentJsonContext.Default.ResidentDockParams));
        var nookId = d!.Data!.Value.GetProperty("nookId").GetString()!;

        await Route(m, "cove://commands/resident.set-height",
            El(new ResidentHeightParams(a.Id, nookId, 240), ResidentJsonContext.Default.ResidentHeightParams));
        Assert.Equal(240, m.Get(a.Id)!.State.Nooks[nookId].ResidentHeight);
    }
}
