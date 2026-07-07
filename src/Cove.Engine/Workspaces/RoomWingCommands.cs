using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Protocol;

namespace Cove.Engine.Workspaces;

public static class RoomWingCommands
{
    [CoveCommand("cove://commands/room.create")]
    public static async Task<ControlResponse> RoomCreate(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RoomWingJsonContext.Default.RoomCreateParams) is not { } p)
            return ctx.Fail("bad_params", "workspaceId is required");
        if (manager.Get(p.WorkspaceId) is not { } actor)
            return ctx.Fail("not_found", $"workspace {p.WorkspaceId} not found");

        var roomId = manager.NewId();
        var paneId = manager.NewId();
        await actor.Mutate(m => WorkspaceInvariants.AddRoom(m, p.WingId ?? WorkspaceModel.MainWingId, roomId, paneId, p.Name ?? "shell")).ConfigureAwait(false);
        return ctx.Ok(new RoomIdResult(roomId), RoomWingJsonContext.Default.RoomIdResult);
    }

    [CoveCommand("cove://commands/room.switch")]
    public static Task<ControlResponse> RoomSwitch(EngineDispatchContext ctx)
        => MutateRoom(ctx, (m, p) => WorkspaceInvariants.SwitchRoom(m, p.RoomId));

    [CoveCommand("cove://commands/room.close")]
    public static Task<ControlResponse> RoomClose(EngineDispatchContext ctx)
        => MutateRoom(ctx, (m, p) => WorkspaceInvariants.CloseRoom(m, p.RoomId, () => System.Guid.NewGuid().ToString("N")));

    [CoveCommand("cove://commands/room.rename")]
    public static async Task<ControlResponse> RoomRename(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RoomWingJsonContext.Default.RoomRenameParams) is not { } p)
            return ctx.Fail("bad_params", "workspaceId, roomId and name are required");
        if (manager.Get(p.WorkspaceId) is not { } actor)
            return ctx.Fail("not_found", $"workspace {p.WorkspaceId} not found");
        await actor.Mutate(m => WorkspaceInvariants.RenameRoom(m, p.RoomId, p.Name)).ConfigureAwait(false);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/room.pin")]
    public static async Task<ControlResponse> RoomPin(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RoomWingJsonContext.Default.RoomPinParams) is not { } p)
            return ctx.Fail("bad_params", "workspaceId and roomId are required");
        if (manager.Get(p.WorkspaceId) is not { } actor)
            return ctx.Fail("not_found", $"workspace {p.WorkspaceId} not found");
        await actor.Mutate(m => WorkspaceInvariants.SetRoomPinned(m, p.RoomId, p.Pinned)).ConfigureAwait(false);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/room.move-to-wing")]
    public static async Task<ControlResponse> RoomMoveToWing(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RoomWingJsonContext.Default.RoomMoveParams) is not { } p)
            return ctx.Fail("bad_params", "workspaceId, roomId and wingId are required");
        if (manager.Get(p.WorkspaceId) is not { } actor)
            return ctx.Fail("not_found", $"workspace {p.WorkspaceId} not found");
        await actor.Mutate(m => WorkspaceInvariants.MoveRoomToWing(m, p.RoomId, p.WingId)).ConfigureAwait(false);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/room.list")]
    public static Task<ControlResponse> RoomList(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return Task.FromResult(ctx.Fail("no_workspaces", "workspace manager unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RoomWingJsonContext.Default.WorkspaceRef) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "workspaceId is required"));
        if (manager.Get(p.WorkspaceId) is not { } actor)
            return Task.FromResult(ctx.Fail("not_found", $"workspace {p.WorkspaceId} not found"));

        var w = actor.State;
        var rooms = w.Rooms.Select(r => new RoomSummary(r.Id, r.Name, r.WingId, r.Pinned, r.Id == w.ActiveRoomId)).ToList();
        return Task.FromResult(ctx.Ok(new RoomListResult(rooms), RoomWingJsonContext.Default.RoomListResult));
    }

    [CoveCommand("cove://commands/wing.create")]
    public static async Task<ControlResponse> WingCreate(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RoomWingJsonContext.Default.WingCreateParams) is not { } p)
            return ctx.Fail("bad_params", "workspaceId and name are required");
        if (manager.Get(p.WorkspaceId) is not { } actor)
            return ctx.Fail("not_found", $"workspace {p.WorkspaceId} not found");
        var wingId = manager.NewId();
        await actor.Mutate(m => WorkspaceInvariants.AddWing(m, wingId, p.Name)).ConfigureAwait(false);
        return ctx.Ok(new WingIdResult(wingId), RoomWingJsonContext.Default.WingIdResult);
    }

    [CoveCommand("cove://commands/wing.rename")]
    public static async Task<ControlResponse> WingRename(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RoomWingJsonContext.Default.WingRenameParams) is not { } p)
            return ctx.Fail("bad_params", "workspaceId, wingId and name are required");
        if (manager.Get(p.WorkspaceId) is not { } actor)
            return ctx.Fail("not_found", $"workspace {p.WorkspaceId} not found");
        await actor.Mutate(m => WorkspaceInvariants.RenameWing(m, p.WingId, p.Name)).ConfigureAwait(false);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/wing.remove")]
    public static async Task<ControlResponse> WingRemove(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RoomWingJsonContext.Default.WingTargetParams) is not { } p)
            return ctx.Fail("bad_params", "workspaceId and wingId are required");
        if (manager.Get(p.WorkspaceId) is not { } actor)
            return ctx.Fail("not_found", $"workspace {p.WorkspaceId} not found");
        await actor.Mutate(m => WorkspaceInvariants.RemoveWing(m, p.WingId, manager.NewId)).ConfigureAwait(false);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/wing.switch")]
    public static async Task<ControlResponse> WingSwitch(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RoomWingJsonContext.Default.WingTargetParams) is not { } p)
            return ctx.Fail("bad_params", "workspaceId and wingId are required");
        if (manager.Get(p.WorkspaceId) is not { } actor)
            return ctx.Fail("not_found", $"workspace {p.WorkspaceId} not found");
        await actor.Mutate(m => WorkspaceInvariants.SwitchWing(m, p.WingId, manager.NewId)).ConfigureAwait(false);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/wing.reorder")]
    public static async Task<ControlResponse> WingReorder(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RoomWingJsonContext.Default.WingReorderParams) is not { } p)
            return ctx.Fail("bad_params", "workspaceId and orderedWingIds are required");
        if (manager.Get(p.WorkspaceId) is not { } actor)
            return ctx.Fail("not_found", $"workspace {p.WorkspaceId} not found");
        await actor.Mutate(m => WorkspaceInvariants.ReorderWings(m, p.OrderedWingIds)).ConfigureAwait(false);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/wing.set-icon")]
    public static async Task<ControlResponse> WingSetIcon(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RoomWingJsonContext.Default.WingIconParams) is not { } p)
            return ctx.Fail("bad_params", "workspaceId and wingId are required");
        if (manager.Get(p.WorkspaceId) is not { } actor)
            return ctx.Fail("not_found", $"workspace {p.WorkspaceId} not found");
        WorkspaceIcon? icon = string.IsNullOrEmpty(p.Kind) ? null : new WorkspaceIcon(p.Kind, p.Value ?? "");
        await actor.Mutate(m => WorkspaceInvariants.SetWingIcon(m, p.WingId, icon)).ConfigureAwait(false);
        return ctx.Ok();
    }
    [CoveCommand("cove://commands/wing.list")]
    public static Task<ControlResponse> WingList(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return Task.FromResult(ctx.Fail("no_workspaces", "workspace manager unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RoomWingJsonContext.Default.WorkspaceRef) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "workspaceId is required"));
        if (manager.Get(p.WorkspaceId) is not { } actor)
            return Task.FromResult(ctx.Fail("not_found", $"workspace {p.WorkspaceId} not found"));
        var wings = actor.State.Wings.Select(w => new WingSummary(w.Id, w.Name, w.Icon)).ToList();
        return Task.FromResult(ctx.Ok(new WingListResult(wings), RoomWingJsonContext.Default.WingListResult));
    }

    private static async Task<ControlResponse> MutateRoom(EngineDispatchContext ctx, Func<WorkspaceModel, RoomTargetParams, WorkspaceModel> op)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RoomWingJsonContext.Default.RoomTargetParams) is not { } p)
            return ctx.Fail("bad_params", "workspaceId and roomId are required");
        if (manager.Get(p.WorkspaceId) is not { } actor)
            return ctx.Fail("not_found", $"workspace {p.WorkspaceId} not found");
        await actor.Mutate(m => op(m, p)).ConfigureAwait(false);
        return ctx.Ok();
    }
}

public sealed record WingReorderParams(string WorkspaceId, IReadOnlyList<string> OrderedWingIds);
public sealed record WingIconParams(string WorkspaceId, string WingId, string? Kind = null, string? Value = null);

public sealed record WorkspaceRef(string WorkspaceId);
public sealed record RoomCreateParams(string WorkspaceId, string? WingId = null, string? Name = null);
public sealed record RoomTargetParams(string WorkspaceId, string RoomId);
public sealed record RoomRenameParams(string WorkspaceId, string RoomId, string Name);
public sealed record RoomPinParams(string WorkspaceId, string RoomId, bool Pinned);
public sealed record RoomMoveParams(string WorkspaceId, string RoomId, string WingId);
public sealed record RoomIdResult(string RoomId);
public sealed record RoomSummary(string Id, string Name, string WingId, bool Pinned, bool Active);
public sealed record RoomListResult(IReadOnlyList<RoomSummary> Rooms);
public sealed record WingCreateParams(string WorkspaceId, string Name);
public sealed record WingTargetParams(string WorkspaceId, string WingId);
public sealed record WingRenameParams(string WorkspaceId, string WingId, string Name);
public sealed record WingIdResult(string WingId);
public sealed record WingSummary(string Id, string Name, WorkspaceIcon? Icon = null);
public sealed record WingListResult(IReadOnlyList<WingSummary> Wings);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WorkspaceRef))]
[JsonSerializable(typeof(RoomCreateParams))]
[JsonSerializable(typeof(RoomTargetParams))]
[JsonSerializable(typeof(RoomRenameParams))]
[JsonSerializable(typeof(RoomPinParams))]
[JsonSerializable(typeof(RoomMoveParams))]
[JsonSerializable(typeof(RoomIdResult))]
[JsonSerializable(typeof(RoomListResult))]
[JsonSerializable(typeof(WingCreateParams))]
[JsonSerializable(typeof(WingTargetParams))]
[JsonSerializable(typeof(WingRenameParams))]
[JsonSerializable(typeof(WingIdResult))]
[JsonSerializable(typeof(WingListResult))]
[JsonSerializable(typeof(WingReorderParams))]
[JsonSerializable(typeof(WingIconParams))]
public sealed partial class RoomWingJsonContext : JsonSerializerContext { }
