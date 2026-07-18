using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Engine.Layout;
using Cove.Persistence;
using Cove.Protocol;

namespace Cove.Engine.Bays;

public static class ShoreWingCommands
{
    private static BayIcon? ToIcon(string? kind, string? value)
        => string.IsNullOrEmpty(kind) ? null : new BayIcon(kind, value ?? "");

    [CoveCommand("cove://commands/shore.create")]
    public static Task<ControlResponse> ShoreCreate(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return Task.FromResult(ctx.Fail("no_bays", "bay manager unavailable"));
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail("not_ready", "layout service unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.ShoreCreateParams) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "bayId is required"));
        if (manager.Get(p.BayId) is null)
            return Task.FromResult(ctx.Fail("not_found", $"bay {p.BayId} not found"));

        var nookId = System.Guid.NewGuid().ToString("N");
        var leaf = new NookLeaf { NookId = nookId, Subtabs = new[] { new Subtab(nookId, NookType.Empty) } };
        var shoreId = layout.CreateShoreInWing(p.BayId, p.WingId ?? LayoutService.MainWingId, p.Name ?? "shell", leaf);
        return Task.FromResult(ctx.Ok(new ShoreIdResult(shoreId), ShoreWingJsonContext.Default.ShoreIdResult));
    }

    [CoveCommand("cove://commands/shore.switch")]
    public static Task<ControlResponse> ShoreSwitch(EngineDispatchContext ctx)
        => MutateShore(ctx, (layout, p) => layout.SwitchShore(p.BayId, p.ShoreId));

    [CoveCommand("cove://commands/shore.close")]
    public static Task<ControlResponse> ShoreClose(EngineDispatchContext ctx)
        => MutateShore(ctx, (layout, p) => layout.CloseShore(p.BayId, p.ShoreId));

    [CoveCommand("cove://commands/shore.rename")]
    public static Task<ControlResponse> ShoreRename(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return Task.FromResult(ctx.Fail("no_bays", "bay manager unavailable"));
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail("not_ready", "layout service unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.ShoreRenameParams) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "bayId, shoreId and name are required"));
        if (manager.Get(p.BayId) is null)
            return Task.FromResult(ctx.Fail("not_found", $"bay {p.BayId} not found"));
        layout.RenameShore(p.BayId, p.ShoreId, p.Name);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/shore.pin")]
    public static Task<ControlResponse> ShorePin(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return Task.FromResult(ctx.Fail("no_bays", "bay manager unavailable"));
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail("not_ready", "layout service unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.ShorePinParams) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "bayId and shoreId are required"));
        if (manager.Get(p.BayId) is null)
            return Task.FromResult(ctx.Fail("not_found", $"bay {p.BayId} not found"));
        layout.SetShorePinned(p.BayId, p.ShoreId, p.Pinned);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/shore.move-to-wing")]
    public static Task<ControlResponse> ShoreMoveToWing(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return Task.FromResult(ctx.Fail("no_bays", "bay manager unavailable"));
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail("not_ready", "layout service unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.ShoreMoveParams) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "bayId, shoreId and wingId are required"));
        if (manager.Get(p.BayId) is null)
            return Task.FromResult(ctx.Fail("not_found", $"bay {p.BayId} not found"));
        layout.MoveShoreToWing(p.BayId, p.ShoreId, p.WingId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/shore.list")]
    public static Task<ControlResponse> ShoreList(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return Task.FromResult(ctx.Fail("no_bays", "bay manager unavailable"));
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail("not_ready", "layout service unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.BayRef) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "bayId is required"));
        if (manager.Get(p.BayId) is null)
            return Task.FromResult(ctx.Fail("not_found", $"bay {p.BayId} not found"));

        var shores = layout.ShoresFor(p.BayId)
            .Select(r => new ShoreSummary(r.Id, r.Name, r.WingId, r.Pinned, r.Active))
            .ToList();
        return Task.FromResult(ctx.Ok(new ShoreListResult(shores), ShoreWingJsonContext.Default.ShoreListResult));
    }

    [CoveCommand("cove://commands/wing.create")]
    public static Task<ControlResponse> WingCreate(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return Task.FromResult(ctx.Fail("no_bays", "bay manager unavailable"));
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail("not_ready", "layout service unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.WingCreateParams) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "bayId and name are required"));
        if (manager.Get(p.BayId) is null)
            return Task.FromResult(ctx.Fail("not_found", $"bay {p.BayId} not found"));
        var wingId = layout.CreateWing(p.BayId, p.Name);
        return Task.FromResult(ctx.Ok(new WingIdResult(wingId), ShoreWingJsonContext.Default.WingIdResult));
    }

    [CoveCommand("cove://commands/wing.rename")]
    public static Task<ControlResponse> WingRename(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return Task.FromResult(ctx.Fail("no_bays", "bay manager unavailable"));
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail("not_ready", "layout service unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.WingRenameParams) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "bayId, wingId and name are required"));
        if (manager.Get(p.BayId) is null)
            return Task.FromResult(ctx.Fail("not_found", $"bay {p.BayId} not found"));
        layout.RenameWing(p.BayId, p.WingId, p.Name);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/wing.remove")]
    public static Task<ControlResponse> WingRemove(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return Task.FromResult(ctx.Fail("no_bays", "bay manager unavailable"));
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail("not_ready", "layout service unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.WingTargetParams) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "bayId and wingId are required"));
        if (manager.Get(p.BayId) is null)
            return Task.FromResult(ctx.Fail("not_found", $"bay {p.BayId} not found"));
        layout.RemoveWing(p.BayId, p.WingId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/wing.switch")]
    public static Task<ControlResponse> WingSwitch(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return Task.FromResult(ctx.Fail("no_bays", "bay manager unavailable"));
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail("not_ready", "layout service unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.WingTargetParams) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "bayId and wingId are required"));
        if (manager.Get(p.BayId) is null)
            return Task.FromResult(ctx.Fail("not_found", $"bay {p.BayId} not found"));
        layout.SwitchWing(p.BayId, p.WingId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/wing.reorder")]
    public static Task<ControlResponse> WingReorder(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return Task.FromResult(ctx.Fail("no_bays", "bay manager unavailable"));
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail("not_ready", "layout service unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.WingReorderParams) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "bayId and orderedWingIds are required"));
        if (manager.Get(p.BayId) is null)
            return Task.FromResult(ctx.Fail("not_found", $"bay {p.BayId} not found"));
        layout.ReorderWings(p.BayId, p.OrderedWingIds);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/wing.set-icon")]
    public static Task<ControlResponse> WingSetIcon(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return Task.FromResult(ctx.Fail("no_bays", "bay manager unavailable"));
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail("not_ready", "layout service unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.WingIconParams) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "bayId and wingId are required"));
        if (manager.Get(p.BayId) is null)
            return Task.FromResult(ctx.Fail("not_found", $"bay {p.BayId} not found"));
        layout.SetWingIcon(p.BayId, p.WingId, p.Kind, p.Value);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/wing.list")]
    public static Task<ControlResponse> WingList(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return Task.FromResult(ctx.Fail("no_bays", "bay manager unavailable"));
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail("not_ready", "layout service unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.BayRef) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "bayId is required"));
        if (manager.Get(p.BayId) is null)
            return Task.FromResult(ctx.Fail("not_found", $"bay {p.BayId} not found"));
        var wings = layout.WingsFor(p.BayId)
            .Select(w => new WingSummary(w.Id, w.Name, ToIcon(w.IconKind, w.IconValue)))
            .ToList();
        return Task.FromResult(ctx.Ok(new WingListResult(wings), ShoreWingJsonContext.Default.WingListResult));
    }

    private static Task<ControlResponse> MutateShore(EngineDispatchContext ctx, System.Action<LayoutService, ShoreTargetParams> op)
    {
        if (ctx.Bays is not { } manager)
            return Task.FromResult(ctx.Fail("no_bays", "bay manager unavailable"));
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail("not_ready", "layout service unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ShoreWingJsonContext.Default.ShoreTargetParams) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "bayId and shoreId are required"));
        if (manager.Get(p.BayId) is null)
            return Task.FromResult(ctx.Fail("not_found", $"bay {p.BayId} not found"));
        op(layout, p);
        return Task.FromResult(ctx.Ok());
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
