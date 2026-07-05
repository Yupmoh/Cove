using System.Text.Json;
using Cove.Protocol;

namespace Cove.Engine.Workspaces;

public static class WorkspaceCommands
{
    [CoveCommand("cove://commands/workspace.create")]
    public static async Task<ControlResponse> WorkspaceCreate(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(WorkspacesJsonContext.Default.WorkspaceCreateParams) is not { } p
            || string.IsNullOrWhiteSpace(p.Name))
            return ctx.Fail("bad_params", "name is required");

        var workspace = await manager.CreateWorkspaceAsync(p.Name, p.ProjectDir ?? "", p.CollectionId).ConfigureAwait(false);
        return ctx.Ok(new WorkspaceIdResult(workspace.Id), WorkspacesJsonContext.Default.WorkspaceIdResult);
    }

    [CoveCommand("cove://commands/workspace.switch")]
    public static async Task<ControlResponse> WorkspaceSwitch(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(WorkspacesJsonContext.Default.WorkspaceIdParams) is not { } p)
            return ctx.Fail("bad_params", "id is required");

        return await manager.SwitchWorkspaceAsync(p.Id).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", $"workspace {p.Id} not found");
    }

    [CoveCommand("cove://commands/workspace.list")]
    public static Task<ControlResponse> WorkspaceList(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return Task.FromResult(ctx.Fail("no_workspaces", "workspace manager unavailable"));
        return Task.FromResult(ctx.Ok(new WorkspaceListResult(manager.ListWorkspaces()), WorkspacesJsonContext.Default.WorkspaceListResult));
    }

    [CoveCommand("cove://commands/workspace.delete")]
    public static async Task<ControlResponse> WorkspaceDelete(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(WorkspacesJsonContext.Default.WorkspaceIdParams) is not { } p)
            return ctx.Fail("bad_params", "id is required");

        return await manager.DeleteWorkspaceAsync(p.Id).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", $"workspace {p.Id} not found");
    }
}
