using System.Text.Json;
using Cove.Engine.Layout;
using Cove.Engine.Protocol;
using NookType = Cove.Persistence.NookType;
using Cove.Protocol;

namespace Cove.Engine;

internal static class NookManyCommands
{
    [CoveCommand("cove://commands/nook.open-many")]
    public static async Task<ControlResponse> OpenMany(EngineDispatchContext ctx)
    {
        if (ctx.Redrive is null)
            return ctx.Fail("not_ready", "command redrive unavailable");
        if (ctx.Request.Params is not JsonElement element
            || element.Deserialize(CoveJsonContext.Default.NookOpenManyParams) is not { } parameters)
        {
            return ctx.Fail("invalid_params", "nook open-many params required");
        }
        var validationError = Validate(parameters);
        if (validationError is not null)
            return ctx.Fail("invalid_params", validationError);

        var opened = new List<NookManyOpenedResult>(parameters.Items.Length);
        var relativeTo = parameters.RelativeToNookId;
        for (var index = 0; index < parameters.Items.Length; index++)
        {
            var item = parameters.Items[index];
            var request = item.NookType == "agent"
                ? AgentRequest(ctx, parameters, item, relativeTo, index)
                : NookRequest(ctx, parameters, item, relativeTo, index);
            var response = await ctx.Redrive(request).ConfigureAwait(false);
            if (response is not { Ok: true, Data: not null })
                return await RollBack(ctx, opened, response, "open failed").ConfigureAwait(false);
            NookManyOpenedResult result;
            if (item.NookType == "agent")
            {
                var agent = response.Data.Value.Deserialize(CoveJsonContext.Default.AgentLaunchResult);
                if (agent is null)
                    return await RollBack(ctx, opened, null, "invalid agent launch response").ConfigureAwait(false);
                result = new NookManyOpenedResult(
                    agent.NookId,
                    "agent",
                    agent.Adapter,
                    agent.BayId,
                    agent.ShoreId,
                    agent.Placement);
            }
            else
            {
                var nook = response.Data.Value.Deserialize(CoveJsonContext.Default.NookOpenResult);
                if (nook is null)
                    return await RollBack(ctx, opened, null, "invalid nook open response").ConfigureAwait(false);
                result = new NookManyOpenedResult(
                    nook.NookId,
                    nook.NookType,
                    null,
                    nook.BayId,
                    nook.ShoreId,
                    nook.Placement);
            }
            opened.Add(result);
            relativeTo = result.NookId;
        }

        NookStackResult? balanceResult = null;
        if (parameters.Balance is not null)
        {
            var stackParams = JsonSerializer.SerializeToElement(
                new NookStackParams(relativeTo, parameters.Balance),
                CoveJsonContext.Default.NookStackParams);
            var stackResponse = await ctx.Redrive(SubRequest(
                ctx,
                "cove://commands/nook.stack",
                stackParams,
                parameters.Items.Length)).ConfigureAwait(false);
            if (stackResponse is not { Ok: true, Data: not null })
                return await RollBack(ctx, opened, stackResponse, "balance failed").ConfigureAwait(false);
            balanceResult = stackResponse.Data.Value.Deserialize(CoveJsonContext.Default.NookStackResult);
            if (balanceResult is null)
                return await RollBack(ctx, opened, null, "invalid stack response").ConfigureAwait(false);
        }

        return ctx.Ok(
            new NookOpenManyResult(opened.ToArray(), balanceResult),
            CoveJsonContext.Default.NookOpenManyResult);
    }

    [CoveCommand("cove://commands/nook.close-others")]
    public static async Task<ControlResponse> CloseOthers(EngineDispatchContext ctx)
    {
        if (ctx.Layout is not { } layout)
            return ctx.Fail("not_ready", "layout service unavailable");
        if (ctx.Redrive is null)
            return ctx.Fail("not_ready", "command redrive unavailable");
        if (ctx.Request.Params is not JsonElement element
            || element.Deserialize(CoveJsonContext.Default.NookCloseOthersParams) is not { } parameters)
        {
            return ctx.Fail("invalid_params", "nook close-others params required");
        }
        if (parameters.Scope is not ("same-shore" or "same-bay"))
            return ctx.Fail("invalid_params", "scope must be same-shore or same-bay");
        var location = layout.ResolveNookLocation(parameters.NookId);
        if (location.BayId is null || location.ShoreId is null)
            return ctx.Fail("not_found", $"unknown or unplaced nook {parameters.NookId}");
        if (parameters.Scope == "same-bay"
            && ctx.Request.CallerNookId is not null
            && ctx.NookScopes?.GetScope(ctx.Request.CallerNookId) is not (McpScope.SameBay or McpScope.All))
        {
            return ctx.Fail("access_denied", "same-bay close exceeds caller scope");
        }

        string[] selected;
        if (parameters.Scope == "same-bay")
        {
            selected = layout.LeafNookIds(location.BayId)
                .Where(nookId => nookId != parameters.NookId)
                .ToArray();
        }
        else
        {
            var root = layout.GetRoot(location.ShoreId);
            selected = root is null
                ? []
                : MosaicOps.Leaves(root)
                    .Where(leaf => leaf.NookId != parameters.NookId
                        && leaf.Subtabs.Count > 0
                        && leaf.Subtabs.Any(subtab => subtab.NookType is NookType.Terminal or NookType.Browser))
                    .Select(leaf => leaf.NookId)
                    .ToArray();
        }

        var closed = new List<NookCloseResult>(selected.Length);
        var failures = new List<string>();
        for (var index = 0; index < selected.Length; index++)
        {
            var closeParams = JsonSerializer.SerializeToElement(
                new NookRefParams(selected[index]),
                CoveJsonContext.Default.NookRefParams);
            var response = await ctx.Redrive(SubRequest(
                ctx,
                "cove://commands/nook.close",
                closeParams,
                index)).ConfigureAwait(false);
            var result = response is { Ok: true, Data: not null }
                ? response.Data.Value.Deserialize(CoveJsonContext.Default.NookCloseResult)
                : null;
            if (result is null)
                failures.Add(selected[index]);
            else
                closed.Add(result);
        }
        layout.FocusNook(location.ShoreId, parameters.NookId);
        if (failures.Count > 0)
            return ctx.Fail("close_failed", $"failed to close: {string.Join(", ", failures)}");
        return ctx.Ok(
            new NookCloseOthersResult(parameters.NookId, closed.ToArray()),
            CoveJsonContext.Default.NookCloseOthersResult);
    }

    private static string? Validate(NookOpenManyParams parameters)
    {
        if (parameters.Items is not { Length: > 0 })
            return "at least one item is required";
        if (string.IsNullOrWhiteSpace(parameters.RelativeToNookId))
            return "relativeToNookId is required";
        if (parameters.Placement is not ("left" or "right" or "above" or "below" or "new-shore"))
            return "placement must be left, right, above, below, or new-shore";
        if (parameters.Balance is not null
            && parameters.Balance is not ("left" or "right" or "above" or "below"))
        {
            return "balance must be left, right, above, or below";
        }
        foreach (var item in parameters.Items)
        {
            if (item.Cols <= 0 || item.Rows <= 0)
                return "cols and rows must be positive";
            var args = item.Args ?? [];
            if (item.NookType == "terminal")
            {
                if (string.IsNullOrWhiteSpace(item.Command) && args.Length > 0)
                    return "terminal args require a command";
                if (item.Url is not null || item.Adapter is not null)
                    return "terminal item contains incompatible fields";
            }
            else if (item.NookType == "browser")
            {
                if (item.Command is not null || args.Length > 0 || item.Adapter is not null)
                    return "browser item contains incompatible fields";
            }
            else if (item.NookType == "agent")
            {
                if (string.IsNullOrWhiteSpace(item.Adapter))
                    return "agent adapter is required";
                if (item.Command is not null || args.Length > 0 || item.Url is not null)
                    return "agent item contains incompatible fields";
            }
            else
            {
                return "nook type must be terminal, browser, or agent";
            }
        }
        return null;
    }

    private static ControlRequest NookRequest(
        EngineDispatchContext ctx,
        NookOpenManyParams parameters,
        NookOpenManyItem item,
        string relativeTo,
        int index)
    {
        var value = new NookOpenParams(
            item.NookType,
            item.Command,
            item.Args ?? [],
            item.Cwd,
            relativeTo,
            parameters.Placement,
            Cols: item.Cols,
            Rows: item.Rows,
            Url: item.Url);
        return SubRequest(
            ctx,
            "cove://commands/nook.open",
            JsonSerializer.SerializeToElement(value, CoveJsonContext.Default.NookOpenParams),
            index);
    }

    private static ControlRequest AgentRequest(
        EngineDispatchContext ctx,
        NookOpenManyParams parameters,
        NookOpenManyItem item,
        string relativeTo,
        int index)
    {
        var value = new AgentLaunchParams(
            "new",
            item.Adapter!,
            item.Profile,
            Cwd: item.Cwd,
            RelativeToNookId: relativeTo,
            Placement: parameters.Placement,
            Name: item.Name,
            Yolo: item.Yolo,
            Cols: item.Cols,
            Rows: item.Rows,
            AccessScope: item.AccessScope,
            Model: item.Model,
            Effort: item.Effort);
        return SubRequest(
            ctx,
            "cove://commands/agent.launch",
            JsonSerializer.SerializeToElement(value, CoveJsonContext.Default.AgentLaunchParams),
            index);
    }

    private static ControlRequest SubRequest(
        EngineDispatchContext ctx,
        string uri,
        JsonElement parameters,
        int index) =>
        new(
            $"{ctx.Request.Id}:{index}",
            uri,
            parameters,
            ctx.Request.Source,
            ctx.Request.CallerNookId);

    private static async Task<ControlResponse> RollBack(
        EngineDispatchContext ctx,
        List<NookManyOpenedResult> opened,
        ControlResponse? failure,
        string fallbackMessage)
    {
        var cleanupFailures = new List<string>();
        for (var index = opened.Count - 1; index >= 0; index--)
        {
            var nookId = opened[index].NookId;
            var parameters = JsonSerializer.SerializeToElement(
                new NookRefParams(nookId),
                CoveJsonContext.Default.NookRefParams);
            var response = await ctx.Redrive!(SubRequest(
                ctx,
                "cove://commands/nook.close",
                parameters,
                index)).ConfigureAwait(false);
            if (response is not { Ok: true })
                cleanupFailures.Add(nookId);
        }
        if (cleanupFailures.Count > 0)
        {
            return ctx.Fail(
                "launch_failed",
                $"{failure?.Error?.Message ?? fallbackMessage}; cleanup failed: {string.Join(", ", cleanupFailures)}");
        }
        return ctx.Fail(
            failure?.Error?.Code ?? "launch_failed",
            failure?.Error?.Message ?? fallbackMessage);
    }
}
