using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Protocol;

namespace Cove.Engine.Bays;

public static class ShoreWingCommands
{
    [CoveCommand("cove://commands/shore.create")]
    public static async Task<ControlResponse> ShoreCreate(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.ShoreCreateParams) is not { } p)
            return ctx.Fail("bad_params", "bayId is required");
        if (manager.Get(p.BayId) is not { } actor)
            return ctx.Fail("not_found", $"bay {p.BayId} not found");

        var shoreId = manager.NewId();
        var nookId = manager.NewId();
        await actor.Mutate(m => BayInvariants.AddShore(m, p.WingId ?? BayModel.MainWingId, shoreId, nookId, p.Name ?? "shell")).ConfigureAwait(false);
        return ctx.Ok(new ShoreIdResult(shoreId), ShoreWingJsonContext.Default.ShoreIdResult);
    }

    [CoveCommand("cove://commands/shore.switch")]
    public static Task<ControlResponse> ShoreSwitch(EngineDispatchContext ctx)
        => MutateShore(ctx, (m, p) => BayInvariants.SwitchShore(m, p.ShoreId));

    [CoveCommand("cove://commands/shore.close")]
    public static Task<ControlResponse> ShoreClose(EngineDispatchContext ctx)
        => MutateShore(ctx, (m, p) => BayInvariants.CloseShore(m, p.ShoreId, () => System.Guid.NewGuid().ToString("N")));

    [CoveCommand("cove://commands/shore.rename")]
    public static async Task<ControlResponse> ShoreRename(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.ShoreRenameParams) is not { } p)
            return ctx.Fail("bad_params", "bayId, shoreId and name are required");
        if (manager.Get(p.BayId) is not { } actor)
            return ctx.Fail("not_found", $"bay {p.BayId} not found");
        await actor.Mutate(m => BayInvariants.RenameShore(m, p.ShoreId, p.Name)).ConfigureAwait(false);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/shore.pin")]
    public static async Task<ControlResponse> ShorePin(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.ShorePinParams) is not { } p)
            return ctx.Fail("bad_params", "bayId and shoreId are required");
        if (manager.Get(p.BayId) is not { } actor)
            return ctx.Fail("not_found", $"bay {p.BayId} not found");
        await actor.Mutate(m => BayInvariants.SetShorePinned(m, p.ShoreId, p.Pinned)).ConfigureAwait(false);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/shore.move-to-wing")]
    public static async Task<ControlResponse> ShoreMoveToWing(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.ShoreMoveParams) is not { } p)
            return ctx.Fail("bad_params", "bayId, shoreId and wingId are required");
        if (manager.Get(p.BayId) is not { } actor)
            return ctx.Fail("not_found", $"bay {p.BayId} not found");
        await actor.Mutate(m => BayInvariants.MoveShoreToWing(m, p.ShoreId, p.WingId)).ConfigureAwait(false);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/shore.list")]
    public static Task<ControlResponse> ShoreList(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return Task.FromResult(ctx.Fail("no_bays", "bay manager unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.BayRef) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "bayId is required"));
        if (manager.Get(p.BayId) is not { } actor)
            return Task.FromResult(ctx.Fail("not_found", $"bay {p.BayId} not found"));

        var w = actor.State;
        var shores = w.Shores.Select(r => new ShoreSummary(r.Id, r.Name, r.WingId, r.Pinned, r.Id == w.ActiveShoreId)).ToList();
        return Task.FromResult(ctx.Ok(new ShoreListResult(shores), ShoreWingJsonContext.Default.ShoreListResult));
    }

    [CoveCommand("cove://commands/wing.create")]
    public static async Task<ControlResponse> WingCreate(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.WingCreateParams) is not { } p)
            return ctx.Fail("bad_params", "bayId and name are required");
        if (manager.Get(p.BayId) is not { } actor)
            return ctx.Fail("not_found", $"bay {p.BayId} not found");
        var wingId = manager.NewId();
        await actor.Mutate(m => BayInvariants.AddWing(m, wingId, p.Name)).ConfigureAwait(false);
        return ctx.Ok(new WingIdResult(wingId), ShoreWingJsonContext.Default.WingIdResult);
    }

    [CoveCommand("cove://commands/wing.rename")]
    public static async Task<ControlResponse> WingRename(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.WingRenameParams) is not { } p)
            return ctx.Fail("bad_params", "bayId, wingId and name are required");
        if (manager.Get(p.BayId) is not { } actor)
            return ctx.Fail("not_found", $"bay {p.BayId} not found");
        await actor.Mutate(m => BayInvariants.RenameWing(m, p.WingId, p.Name)).ConfigureAwait(false);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/wing.remove")]
    public static async Task<ControlResponse> WingRemove(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.WingTargetParams) is not { } p)
            return ctx.Fail("bad_params", "bayId and wingId are required");
        if (manager.Get(p.BayId) is not { } actor)
            return ctx.Fail("not_found", $"bay {p.BayId} not found");
        await actor.Mutate(m => BayInvariants.RemoveWing(m, p.WingId, manager.NewId)).ConfigureAwait(false);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/wing.switch")]
    public static async Task<ControlResponse> WingSwitch(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.WingTargetParams) is not { } p)
            return ctx.Fail("bad_params", "bayId and wingId are required");
        if (manager.Get(p.BayId) is not { } actor)
            return ctx.Fail("not_found", $"bay {p.BayId} not found");
        await actor.Mutate(m => BayInvariants.SwitchWing(m, p.WingId, manager.NewId)).ConfigureAwait(false);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/wing.reorder")]
    public static async Task<ControlResponse> WingReorder(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.WingReorderParams) is not { } p)
            return ctx.Fail("bad_params", "bayId and orderedWingIds are required");
        if (manager.Get(p.BayId) is not { } actor)
            return ctx.Fail("not_found", $"bay {p.BayId} not found");
        await actor.Mutate(m => BayInvariants.ReorderWings(m, p.OrderedWingIds)).ConfigureAwait(false);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/wing.set-icon")]
    public static async Task<ControlResponse> WingSetIcon(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.WingIconParams) is not { } p)
            return ctx.Fail("bad_params", "bayId and wingId are required");
        if (manager.Get(p.BayId) is not { } actor)
            return ctx.Fail("not_found", $"bay {p.BayId} not found");
        BayIcon? icon = string.IsNullOrEmpty(p.Kind) ? null : new BayIcon(p.Kind, p.Value ?? "");
        await actor.Mutate(m => BayInvariants.SetWingIcon(m, p.WingId, icon)).ConfigureAwait(false);
        return ctx.Ok();
    }
    [CoveCommand("cove://commands/wing.list")]
    public static Task<ControlResponse> WingList(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return Task.FromResult(ctx.Fail("no_bays", "bay manager unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.BayRef) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "bayId is required"));
        if (manager.Get(p.BayId) is not { } actor)
            return Task.FromResult(ctx.Fail("not_found", $"bay {p.BayId} not found"));
        var wings = actor.State.Wings.Select(w => new WingSummary(w.Id, w.Name, w.Icon)).ToList();
        return Task.FromResult(ctx.Ok(new WingListResult(wings), ShoreWingJsonContext.Default.WingListResult));
    }

    private static async Task<ControlResponse> MutateShore(EngineDispatchContext ctx, Func<BayModel, ShoreTargetParams, BayModel> op)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.ShoreTargetParams) is not { } p)
            return ctx.Fail("bad_params", "bayId and shoreId are required");
        if (manager.Get(p.BayId) is not { } actor)
            return ctx.Fail("not_found", $"bay {p.BayId} not found");
        await actor.Mutate(m => op(m, p)).ConfigureAwait(false);
        return ctx.Ok();
    }
}

public sealed record WingReorderParams(string BayId, IReadOnlyList<string> OrderedWingIds);
public sealed record WingIconParams(string BayId, string WingId, string? Kind = null, string? Value = null);

public sealed record BayRef(string BayId);
public sealed record ShoreCreateParams(string BayId, string? WingId = null, string? Name = null);
public sealed record ShoreTargetParams(string BayId, string ShoreId);
public sealed record ShoreRenameParams(string BayId, string ShoreId, string Name);
public sealed record ShorePinParams(string BayId, string ShoreId, bool Pinned);
public sealed record ShoreMoveParams(string BayId, string ShoreId, string WingId);
public sealed record ShoreIdResult(string ShoreId);
public sealed record ShoreSummary(string Id, string Name, string WingId, bool Pinned, bool Active);
public sealed record ShoreListResult(IReadOnlyList<ShoreSummary> Shores);
public sealed record WingCreateParams(string BayId, string Name);
public sealed record WingTargetParams(string BayId, string WingId);
public sealed record WingRenameParams(string BayId, string WingId, string Name);
public sealed record WingIdResult(string WingId);
public sealed record WingSummary(string Id, string Name, BayIcon? Icon = null);
public sealed record WingListResult(IReadOnlyList<WingSummary> Wings);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BayRef))]
[JsonSerializable(typeof(ShoreCreateParams))]
[JsonSerializable(typeof(ShoreTargetParams))]
[JsonSerializable(typeof(ShoreRenameParams))]
[JsonSerializable(typeof(ShorePinParams))]
[JsonSerializable(typeof(ShoreMoveParams))]
[JsonSerializable(typeof(ShoreIdResult))]
[JsonSerializable(typeof(ShoreListResult))]
[JsonSerializable(typeof(WingCreateParams))]
[JsonSerializable(typeof(WingTargetParams))]
[JsonSerializable(typeof(WingRenameParams))]
[JsonSerializable(typeof(WingIdResult))]
[JsonSerializable(typeof(WingListResult))]
[JsonSerializable(typeof(WingReorderParams))]
[JsonSerializable(typeof(WingIconParams))]
public sealed partial class ShoreWingJsonContext : JsonSerializerContext { }
