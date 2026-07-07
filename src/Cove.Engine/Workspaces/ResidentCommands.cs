using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Protocol;

namespace Cove.Engine.Workspaces;

public static class ResidentCommands
{
    [CoveCommand("cove://commands/resident.dock")]
    public static async Task<ControlResponse> ResidentDock(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ResidentJsonContext.Default.ResidentDockParams) is not { } p)
            return ctx.Fail("bad_params", "workspaceId, scope and slot are required");
        var scope = p.Scope is "workspace" or "global" ? p.Scope : "workspace";
        var paneId = await manager.DockResidentAsync(p.WorkspaceId, p.PaneId, scope, p.Slot).ConfigureAwait(false);
        return paneId is null
            ? ctx.Fail("not_found", $"workspace {p.WorkspaceId} not found")
            : ctx.Ok(new ResidentDockResult(paneId), ResidentJsonContext.Default.ResidentDockResult);
    }

    [CoveCommand("cove://commands/resident.undock")]
    public static async Task<ControlResponse> ResidentUndock(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ResidentJsonContext.Default.ResidentTargetParams) is not { } p)
            return ctx.Fail("bad_params", "workspaceId and paneId are required");
        return await manager.UndockResidentAsync(p.WorkspaceId, p.PaneId).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", "workspace or pane not found");
    }

    [CoveCommand("cove://commands/resident.set-collapsed")]
    public static async Task<ControlResponse> ResidentSetCollapsed(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ResidentJsonContext.Default.ResidentCollapseParams) is not { } p)
            return ctx.Fail("bad_params", "workspaceId and paneId are required");
        return await manager.SetResidentCollapsedAsync(p.WorkspaceId, p.PaneId, p.Collapsed).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", "workspace or pane not found");
    }

    [CoveCommand("cove://commands/resident.set-height")]
    public static async Task<ControlResponse> ResidentSetHeight(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ResidentJsonContext.Default.ResidentHeightParams) is not { } p)
            return ctx.Fail("bad_params", "workspaceId and paneId are required");
        return await manager.SetResidentHeightAsync(p.WorkspaceId, p.PaneId, p.Height).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", "workspace or pane not found");
    }

    [CoveCommand("cove://commands/resident.list")]
    public static Task<ControlResponse> ResidentList(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return Task.FromResult(ctx.Fail("no_workspaces", "workspace manager unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ResidentJsonContext.Default.ResidentListParams) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "workspaceId is required"));
        return Task.FromResult(ctx.Ok(new ResidentListResult(manager.ListResidents(p.WorkspaceId)), ResidentJsonContext.Default.ResidentListResult));
    }
}

public sealed record ResidentDockParams(string WorkspaceId, string? PaneId, string Scope, int Slot);
public sealed record ResidentTargetParams(string WorkspaceId, string PaneId);
public sealed record ResidentCollapseParams(string WorkspaceId, string PaneId, bool Collapsed);
public sealed record ResidentHeightParams(string WorkspaceId, string PaneId, int Height);
public sealed record ResidentListParams(string WorkspaceId);
public sealed record ResidentDockResult(string PaneId);
public sealed record ResidentSummary(string PaneId, string WorkspaceId, string Scope, int Slot, bool Collapsed);
public sealed record ResidentListResult(IReadOnlyList<ResidentSummary> Residents);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ResidentDockParams))]
[JsonSerializable(typeof(ResidentTargetParams))]
[JsonSerializable(typeof(ResidentCollapseParams))]
[JsonSerializable(typeof(ResidentHeightParams))]
[JsonSerializable(typeof(ResidentListParams))]
[JsonSerializable(typeof(ResidentDockResult))]
[JsonSerializable(typeof(ResidentListResult))]
public sealed partial class ResidentJsonContext : JsonSerializerContext { }
