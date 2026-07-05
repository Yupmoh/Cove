using System.Text.Json;
using Cove.Engine;
using Cove.Engine.Workspaces;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class WorkspaceCommandsTests
{
    private static Task<ControlResponse?> Route(WorkspaceManager manager, string uri, JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, null, manager);

    [Fact]
    public async Task Create_List_Switch_Delete_WorkHeadless()
    {
        var changes = new List<WorkspaceChange>();
        int n = 0;
        await using var manager = new WorkspaceManager(emit: changes.Add, newId: () => $"id-{++n}");

        var createParams = JsonSerializer.SerializeToElement(
            new WorkspaceCreateParams("proj-a", "/tmp/a"), WorkspacesJsonContext.Default.WorkspaceCreateParams);
        var created = await Route(manager, "cove://commands/workspace.create", createParams);
        Assert.True(created!.Ok);
        var id = created.Data!.Value.GetProperty("id").GetString()!;

        var listed = await Route(manager, "cove://commands/workspace.list", null);
        Assert.True(listed!.Ok);
        var workspaces = listed.Data!.Value.GetProperty("workspaces");
        Assert.Equal(1, workspaces.GetArrayLength());
        Assert.Equal("proj-a", workspaces[0].GetProperty("name").GetString());
        Assert.True(workspaces[0].GetProperty("active").GetBoolean());

        var switchParams = JsonSerializer.SerializeToElement(
            new WorkspaceIdParams(id), WorkspacesJsonContext.Default.WorkspaceIdParams);
        var switched = await Route(manager, "cove://commands/workspace.switch", switchParams);
        Assert.True(switched!.Ok);

        var deleted = await Route(manager, "cove://commands/workspace.delete", switchParams);
        Assert.True(deleted!.Ok);

        var listed2 = await Route(manager, "cove://commands/workspace.list", null);
        Assert.Equal(0, listed2!.Data!.Value.GetProperty("workspaces").GetArrayLength());

        Assert.Contains(changes, c => c.Kind == WorkspaceChangeKind.Created);
        Assert.Contains(changes, c => c.Kind == WorkspaceChangeKind.Deleted);
    }

    [Fact]
    public async Task Switch_UnknownWorkspace_Fails()
    {
        await using var manager = new WorkspaceManager();
        var prm = JsonSerializer.SerializeToElement(
            new WorkspaceIdParams("nope"), WorkspacesJsonContext.Default.WorkspaceIdParams);
        var resp = await Route(manager, "cove://commands/workspace.switch", prm);
        Assert.False(resp!.Ok);
        Assert.Equal("not_found", resp.Error!.Code);
    }

    [Fact]
    public async Task Create_MissingName_Fails()
    {
        await using var manager = new WorkspaceManager();
        var prm = JsonSerializer.SerializeToElement(
            new WorkspaceCreateParams("", "/tmp"), WorkspacesJsonContext.Default.WorkspaceCreateParams);
        var resp = await Route(manager, "cove://commands/workspace.create", prm);
        Assert.False(resp!.Ok);
        Assert.Equal("bad_params", resp.Error!.Code);
    }
}
