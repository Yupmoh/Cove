using System.Text.Json;
using Cove.Engine.Layout;
using Cove.Persistence;
using Cove.Protocol;

namespace Cove.Engine;

internal static class NookCloseCommands
{
    [CoveCommand("cove://commands/nook.close")]
    public static Task<ControlResponse> Close(EngineDispatchContext ctx)
    {
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail("not_ready", "layout service unavailable"));
        if (ctx.Request.Params is not JsonElement element
            || element.Deserialize(Cove.Protocol.CoveJsonContext.Default.NookRefParams) is not { } parameters)
        {
            return Task.FromResult(ctx.Fail("invalid_params", "nook ref required"));
        }
        var location = layout.ResolveNookLocation(parameters.NookId);
        if (location.BayId is null || location.ShoreId is null)
            return Task.FromResult(ctx.Fail("not_found", $"unknown or unplaced nook {parameters.NookId}"));
        var root = layout.GetRoot(location.ShoreId);
        var leaf = root is null
            ? null
            : MosaicOps.Find(root, parameters.NookId);
        if (leaf is null || leaf.Subtabs.Count == 0)
            return Task.FromResult(ctx.Fail("not_found", $"unknown nook {parameters.NookId}"));
        var activeIndex = Math.Clamp(leaf.ActiveSubtab, 0, leaf.Subtabs.Count - 1);
        var nookType = leaf.Subtabs[activeIndex].NookType;
        if (nookType == NookType.Browser)
        {
            if (ctx.Browser is not { } browser)
                return Task.FromResult(ctx.Fail("not_ready", "browser manager unavailable"));
            if (browser.Get(parameters.NookId) is null)
                return Task.FromResult(ctx.Fail("not_found", $"browser nook state missing {parameters.NookId}"));
            try
            {
                layout.CloseNook(location.ShoreId, parameters.NookId);
                browser.Close(parameters.NookId);
                return Task.FromResult(Result(ctx, parameters.NookId, "browser", location));
            }
            catch (Exception exception)
            {
                return Task.FromResult(ctx.Fail("close_failed", exception.Message));
            }
        }
        if (nookType != NookType.Terminal)
            return Task.FromResult(ctx.Fail("invalid_state", $"nook type {nookType} is not closeable"));
        if (ctx.Nooks is not { } nooks)
            return Task.FromResult(ctx.Fail("not_ready", "nook registry unavailable"));
        if (!nooks.List().Any(nook => nook.NookId == parameters.NookId))
            return Task.FromResult(ctx.Fail("not_found", $"terminal nook state missing {parameters.NookId}"));
        try
        {
            layout.CloseNook(location.ShoreId, parameters.NookId);
            ctx.Lifecycle?.Close(parameters.NookId);
            ctx.AgentRouter?.Unregister(parameters.NookId);
            ctx.Sessions?.Unregister(parameters.NookId);
            ctx.Launcher?.ClearOverrides(parameters.NookId);
            ctx.NookScopes?.ClearScope(parameters.NookId);
            if (!nooks.Kill(parameters.NookId))
                return Task.FromResult(ctx.Fail("close_failed", $"terminal nook close failed {parameters.NookId}"));
            return Task.FromResult(Result(ctx, parameters.NookId, "terminal", location));
        }
        catch (Exception exception)
        {
            return Task.FromResult(ctx.Fail("close_failed", exception.Message));
        }
    }

    private static ControlResponse Result(
        EngineDispatchContext ctx,
        string nookId,
        string nookType,
        (string? BayId, string? ShoreId) location) =>
        ctx.Ok(
            new NookCloseResult(
                nookId,
                nookType,
                location.BayId!,
                location.ShoreId!),
            Cove.Protocol.CoveJsonContext.Default.NookCloseResult);
}
