using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Engine;
using Cove.Engine.Workspaces;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class WorkspaceRenameCommandTests
{
    private static Task<ControlResponse?> Route(WorkspaceManager m, string uri, JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, null, m);

    private static JsonElement El<T>(T v, JsonTypeInfo<T> ti) => JsonSerializer.SerializeToElement(v, ti);

    [Fact]
    public async Task Rename_UpdatesNameAndSurfacesInList()
    {
        int n = 0;
        await using var m = new WorkspaceManager(newId: () => $"id-{++n}");
        var ws = await m.CreateWorkspaceAsync("old", "/a");

        var resp = await Route(m, "cove://commands/workspace.rename",
            El(new WorkspaceRenameParams(ws.Id, "new"), WorkspaceExtraJsonContext.Default.WorkspaceRenameParams));

        Assert.True(resp!.Ok);
        Assert.Equal("new", m.Get(ws.Id)!.State.Name);
        Assert.Contains(m.ListWorkspaces(), w => w.Id == ws.Id && w.Name == "new");
    }

    [Fact]
    public async Task Rename_EmptyName_IsInvalidParams()
    {
        int n = 0;
        await using var m = new WorkspaceManager(newId: () => $"id-{++n}");
        var ws = await m.CreateWorkspaceAsync("old", "/a");

        var resp = await Route(m, "cove://commands/workspace.rename",
            El(new WorkspaceRenameParams(ws.Id, "   "), WorkspaceExtraJsonContext.Default.WorkspaceRenameParams));

        Assert.False(resp!.Ok);
        Assert.Equal("invalid_params", resp.Error!.Code);
        Assert.Equal("old", m.Get(ws.Id)!.State.Name);
    }

    [Fact]
    public async Task Rename_UnknownWorkspace_IsNotFound()
    {
        await using var m = new WorkspaceManager(newId: () => "id-1");

        var resp = await Route(m, "cove://commands/workspace.rename",
            El(new WorkspaceRenameParams("missing", "new"), WorkspaceExtraJsonContext.Default.WorkspaceRenameParams));

        Assert.False(resp!.Ok);
        Assert.Equal("not_found", resp.Error!.Code);
    }
}
