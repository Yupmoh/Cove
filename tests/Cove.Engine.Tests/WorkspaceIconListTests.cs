using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Engine;
using Cove.Engine.Workspaces;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class WorkspaceIconListTests
{
    private static Task<ControlResponse?> Route(WorkspaceManager m, string uri, JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, null, m);

    private static JsonElement El<T>(T v, JsonTypeInfo<T> ti) => JsonSerializer.SerializeToElement(v, ti);

    private static WorkspaceSummary ListItem(ControlResponse resp, string id) =>
        resp.Data!.Value.Deserialize(WorkspacesJsonContext.Default.WorkspaceListResult)!
            .Workspaces.Single(w => w.Id == id);

    [Fact]
    public async Task List_CarriesIcon_AfterSetIcon()
    {
        int n = 0;
        await using var m = new WorkspaceManager(newId: () => $"id-{++n}");
        var ws = await m.CreateWorkspaceAsync("w", "/a");

        var setResp = await Route(m, "cove://commands/workspace.set-icon",
            El(new WorkspaceIconParams(ws.Id, "emoji", "🚀"), WorkspaceExtraJsonContext.Default.WorkspaceIconParams));
        Assert.True(setResp!.Ok);

        var listResp = await Route(m, "cove://commands/workspace.list", null);
        var item = ListItem(listResp!, ws.Id);
        Assert.Equal("emoji", item.IconKind);
        Assert.Equal("🚀", item.IconValue);
    }

    [Fact]
    public async Task List_IconNull_WhenNoIcon()
    {
        int n = 0;
        await using var m = new WorkspaceManager(newId: () => $"id-{++n}");
        var ws = await m.CreateWorkspaceAsync("w", "/a");

        var listResp = await Route(m, "cove://commands/workspace.list", null);
        var item = ListItem(listResp!, ws.Id);
        Assert.Null(item.IconKind);
        Assert.Null(item.IconValue);
    }

    [Fact]
    public async Task List_IconCleared_WhenSetIconEmptyKind()
    {
        int n = 0;
        await using var m = new WorkspaceManager(newId: () => $"id-{++n}");
        var ws = await m.CreateWorkspaceAsync("w", "/a");

        await Route(m, "cove://commands/workspace.set-icon",
            El(new WorkspaceIconParams(ws.Id, "emoji", "🚀"), WorkspaceExtraJsonContext.Default.WorkspaceIconParams));
        await Route(m, "cove://commands/workspace.set-icon",
            El(new WorkspaceIconParams(ws.Id, null, null), WorkspaceExtraJsonContext.Default.WorkspaceIconParams));

        var listResp = await Route(m, "cove://commands/workspace.list", null);
        var item = ListItem(listResp!, ws.Id);
        Assert.Null(item.IconKind);
        Assert.Null(item.IconValue);
    }
}
