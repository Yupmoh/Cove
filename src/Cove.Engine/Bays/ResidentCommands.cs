using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Protocol;

namespace Cove.Engine.Bays;

public static class ResidentCommands
{
    [CoveCommand("cove://commands/resident.dock")]
    public static async Task<ControlResponse> ResidentDock(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ResidentJsonContext.Default.ResidentDockParams) is not { } p)
            return ctx.Fail("bad_params", "bayId, scope and slot are required");
        var scope = p.Scope is "bay" or "global" ? p.Scope : "bay";
        var nookId = await manager.DockResidentAsync(p.BayId, p.NookId, scope, p.Slot).ConfigureAwait(false);
        return nookId is null
            ? ctx.Fail("not_found", $"bay {p.BayId} not found")
            : ctx.Ok(new ResidentDockResult(nookId), ResidentJsonContext.Default.ResidentDockResult);
    }

    [CoveCommand("cove://commands/resident.undock")]
    public static async Task<ControlResponse> ResidentUndock(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ResidentJsonContext.Default.ResidentTargetParams) is not { } p)
            return ctx.Fail("bad_params", "bayId and nookId are required");
        return await manager.UndockResidentAsync(p.BayId, p.NookId).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", "bay or nook not found");
    }

    [CoveCommand("cove://commands/resident.set-collapsed")]
    public static async Task<ControlResponse> ResidentSetCollapsed(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ResidentJsonContext.Default.ResidentCollapseParams) is not { } p)
            return ctx.Fail("bad_params", "bayId and nookId are required");
        return await manager.SetResidentCollapsedAsync(p.BayId, p.NookId, p.Collapsed).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", "bay or nook not found");
    }

    [CoveCommand("cove://commands/resident.set-height")]
    public static async Task<ControlResponse> ResidentSetHeight(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ResidentJsonContext.Default.ResidentHeightParams) is not { } p)
            return ctx.Fail("bad_params", "bayId and nookId are required");
        return await manager.SetResidentHeightAsync(p.BayId, p.NookId, p.Height).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", "bay or nook not found");
    }

    [CoveCommand("cove://commands/resident.list")]
    public static Task<ControlResponse> ResidentList(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return Task.FromResult(ctx.Fail("no_bays", "bay manager unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(ResidentJsonContext.Default.ResidentListParams) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "bayId is required"));
        return Task.FromResult(ctx.Ok(new ResidentListResult(manager.ListResidents(p.BayId)), ResidentJsonContext.Default.ResidentListResult));
    }
}

public sealed record ResidentDockParams(string BayId, string? NookId, string Scope, int Slot);
public sealed record ResidentTargetParams(string BayId, string NookId);
public sealed record ResidentCollapseParams(string BayId, string NookId, bool Collapsed);
public sealed record ResidentHeightParams(string BayId, string NookId, int Height);
public sealed record ResidentListParams(string BayId);
public sealed record ResidentDockResult(string NookId);
public sealed record ResidentSummary(string NookId, string BayId, string Scope, int Slot, bool Collapsed);
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
