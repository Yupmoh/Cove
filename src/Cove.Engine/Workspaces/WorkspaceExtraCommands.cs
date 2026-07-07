using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Protocol;

namespace Cove.Engine.Workspaces;

public static class WorkspaceExtraCommands
{
    [CoveCommand("cove://commands/workspace.hide")]
    public static async Task<ControlResponse> WorkspaceHide(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(WorkspaceExtraJsonContext.Default.WorkspaceHideParams) is not { } p)
            return ctx.Fail("bad_params", "id is required");
        return await manager.SetWorkspaceHiddenAsync(p.Id, p.Hidden).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", $"workspace {p.Id} not found");
    }

    [CoveCommand("cove://commands/workspace.reorder")]
    public static async Task<ControlResponse> WorkspaceReorder(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(WorkspaceExtraJsonContext.Default.WorkspaceReorderParams) is not { } p)
            return ctx.Fail("bad_params", "orderedIds is required");
        await manager.ReorderWorkspacesAsync(p.OrderedIds).ConfigureAwait(false);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/workspace.set-icon")]
    public static async Task<ControlResponse> WorkspaceSetIcon(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(WorkspaceExtraJsonContext.Default.WorkspaceIconParams) is not { } p)
            return ctx.Fail("bad_params", "id is required");
        WorkspaceIcon? icon = string.IsNullOrEmpty(p.Kind) ? null : new WorkspaceIcon(p.Kind, p.Value ?? "");
        return await manager.SetWorkspaceIconAsync(p.Id, icon).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", $"workspace {p.Id} not found");
    }

    [CoveCommand("cove://commands/workspace.set-accent")]
    public static async Task<ControlResponse> WorkspaceSetAccent(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(WorkspaceExtraJsonContext.Default.WorkspaceAccentParams) is not { } p)
            return ctx.Fail("bad_params", "id is required");
        return await manager.SetWorkspaceAccentAsync(p.Id, p.Accent).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", $"workspace {p.Id} not found");
    }

    [CoveCommand("cove://commands/workspace.move-room")]
    public static async Task<ControlResponse> WorkspaceMoveRoom(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(WorkspaceExtraJsonContext.Default.WorkspaceMoveRoomParams) is not { } p)
            return ctx.Fail("bad_params", "fromWorkspaceId, roomId and toWorkspaceId are required");
        return await manager.MoveRoomAsync(p.FromWorkspaceId, p.RoomId, p.ToWorkspaceId).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", "workspace or room not found");
    }
}

public sealed record WorkspaceHideParams(string Id, bool Hidden);
public sealed record WorkspaceReorderParams(IReadOnlyList<string> OrderedIds);
public sealed record WorkspaceIconParams(string Id, string? Kind = null, string? Value = null);
public sealed record WorkspaceAccentParams(string Id, string? Accent = null);
public sealed record WorkspaceMoveRoomParams(string FromWorkspaceId, string RoomId, string ToWorkspaceId);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WorkspaceHideParams))]
[JsonSerializable(typeof(WorkspaceReorderParams))]
[JsonSerializable(typeof(WorkspaceIconParams))]
[JsonSerializable(typeof(WorkspaceAccentParams))]
[JsonSerializable(typeof(WorkspaceMoveRoomParams))]
public sealed partial class WorkspaceExtraJsonContext : JsonSerializerContext { }
