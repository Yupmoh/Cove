using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Protocol;

namespace Cove.Engine.Bays;

public static class BayExtraCommands
{
    [CoveCommand("cove://commands/bay.hide")]
    public static async Task<ControlResponse> BayHide(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(BayExtraJsonContext.Default.BayHideParams) is not { } p)
            return ctx.Fail("bad_params", "id is required");
        return await manager.SetBayHiddenAsync(p.Id, p.Hidden).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", $"bay {p.Id} not found");
    }

    [CoveCommand("cove://commands/bay.rename")]
    public static async Task<ControlResponse> BayRename(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("not_ready", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(BayExtraJsonContext.Default.BayRenameParams) is not { } p)
            return ctx.Fail("invalid_params", "id and name are required");
        if (string.IsNullOrEmpty(p.Id))
            return ctx.Fail("invalid_params", "id is required");
        if (string.IsNullOrWhiteSpace(p.Name))
            return ctx.Fail("invalid_params", "name is required");
        return await manager.RenameBayAsync(p.Id, p.Name.Trim()).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", $"bay {p.Id} not found");
    }

    [CoveCommand("cove://commands/bay.reorder")]
    public static async Task<ControlResponse> BayReorder(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(BayExtraJsonContext.Default.BayReorderParams) is not { } p)
            return ctx.Fail("bad_params", "orderedIds is required");
        await manager.ReorderBaysAsync(p.OrderedIds).ConfigureAwait(false);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/bay.set-icon")]
    public static async Task<ControlResponse> BaySetIcon(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(BayExtraJsonContext.Default.BayIconParams) is not { } p)
            return ctx.Fail("bad_params", "id is required");
        BayIcon? icon = string.IsNullOrEmpty(p.Kind) ? null : new BayIcon(p.Kind, p.Value ?? "");
        return await manager.SetBayIconAsync(p.Id, icon).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", $"bay {p.Id} not found");
    }

    [CoveCommand("cove://commands/bay.set-accent")]
    public static async Task<ControlResponse> BaySetAccent(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(BayExtraJsonContext.Default.BayAccentParams) is not { } p)
            return ctx.Fail("bad_params", "id is required");
        return await manager.SetBayAccentAsync(p.Id, p.Accent).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", $"bay {p.Id} not found");
    }

    [CoveCommand("cove://commands/bay.move-shore")]
    public static async Task<ControlResponse> BayMoveShore(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(BayExtraJsonContext.Default.BayMoveShoreParams) is not { } p)
            return ctx.Fail("bad_params", "fromBayId, shoreId and toBayId are required");
        return await manager.MoveShoreAsync(p.FromBayId, p.ShoreId, p.ToBayId).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", "bay or shore not found");
    }
}

public sealed record BayHideParams(string Id, bool Hidden);
public sealed record BayRenameParams(string Id, string Name);
public sealed record BayReorderParams(IReadOnlyList<string> OrderedIds);
public sealed record BayIconParams(string Id, string? Kind = null, string? Value = null);
public sealed record BayAccentParams(string Id, string? Accent = null);
public sealed record BayMoveShoreParams(string FromBayId, string ShoreId, string ToBayId);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BayHideParams))]
[JsonSerializable(typeof(BayRenameParams))]
[JsonSerializable(typeof(BayReorderParams))]
[JsonSerializable(typeof(BayIconParams))]
[JsonSerializable(typeof(BayAccentParams))]
[JsonSerializable(typeof(BayMoveShoreParams))]
public sealed partial class BayExtraJsonContext : JsonSerializerContext { }
