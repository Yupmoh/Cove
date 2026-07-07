using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Engine;
using Cove.Engine.Workspaces;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ResidentCommandsTests
{
    private static Task<ControlResponse?> Route(WorkspaceManager m, string uri, JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, null, m);

    private static JsonElement El<T>(T v, JsonTypeInfo<T> ti) => JsonSerializer.SerializeToElement(v, ti);

    private static async Task<List<string?>> ResidentIds(WorkspaceManager m, string wsId)
    {
        var listed = await Route(m, "cove://commands/resident.list",
            El(new ResidentListParams(wsId), ResidentJsonContext.Default.ResidentListParams));
        var arr = listed!.Data!.Value.GetProperty("residents");
        return Enumerable.Range(0, arr.GetArrayLength()).Select(i => arr[i].GetProperty("paneId").GetString()).ToList();
    }

    [Fact]
    public async Task Residents_WorkspaceScoped_And_Global_Resolution()
    {
        int n = 0;
        await using var m = new WorkspaceManager(newId: () => $"id-{++n}");
        var a = await m.CreateWorkspaceAsync("a", "/a");
        var b = await m.CreateWorkspaceAsync("b", "/b");

        var d1 = await Route(m, "cove://commands/resident.dock",
            El(new ResidentDockParams(a.Id, null, "workspace", 0), ResidentJsonContext.Default.ResidentDockParams));
        var wsPane = d1!.Data!.Value.GetProperty("paneId").GetString()!;

        var d2 = await Route(m, "cove://commands/resident.dock",
            El(new ResidentDockParams(b.Id, null, "global", 1), ResidentJsonContext.Default.ResidentDockParams));
        var globalPane = d2!.Data!.Value.GetProperty("paneId").GetString()!;

        var idsA = await ResidentIds(m, a.Id);
        Assert.Contains(wsPane, idsA);
        Assert.Contains(globalPane, idsA);

        var idsB = await ResidentIds(m, b.Id);
        Assert.DoesNotContain(wsPane, idsB);
        Assert.Contains(globalPane, idsB);

        await Route(m, "cove://commands/resident.undock",
            El(new ResidentTargetParams(a.Id, wsPane), ResidentJsonContext.Default.ResidentTargetParams));
        var idsA2 = await ResidentIds(m, a.Id);
        Assert.DoesNotContain(wsPane, idsA2);
        Assert.Contains(globalPane, idsA2);
    }

    [Fact]
    public async Task Resident_SetCollapsed_Persists()
    {
        int n = 0;
        await using var m = new WorkspaceManager(newId: () => $"id-{++n}");
        var a = await m.CreateWorkspaceAsync("a", "/a");
        var d = await Route(m, "cove://commands/resident.dock",
            El(new ResidentDockParams(a.Id, null, "workspace", 0), ResidentJsonContext.Default.ResidentDockParams));
        var paneId = d!.Data!.Value.GetProperty("paneId").GetString()!;

        await Route(m, "cove://commands/resident.set-collapsed",
            El(new ResidentCollapseParams(a.Id, paneId, true), ResidentJsonContext.Default.ResidentCollapseParams));
        Assert.True(m.Get(a.Id)!.State.Panes[paneId].ResidentCollapsed);
    }
}
