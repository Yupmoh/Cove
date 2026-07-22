using System.Text.Json;
using Cove.Engine.Layout;
using Cove.Engine.Pty;
using Cove.Persistence;
using Cove.Protocol;

namespace Cove.Engine;

internal static class NookOpenCommands
{
    [CoveCommand("cove://commands/nook.open")]
    public static Task<ControlResponse> Open(EngineDispatchContext ctx)
    {
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail("not_ready", "layout service unavailable"));
        if (ctx.Request.Params is not JsonElement element
            || element.Deserialize(Cove.Protocol.CoveJsonContext.Default.NookOpenParams) is not { } parameters)
        {
            return Task.FromResult(ctx.Fail("invalid_params", "nook open params required"));
        }
        if (parameters.NookType is not ("terminal" or "browser"))
            return Task.FromResult(ctx.Fail("invalid_params", "nook type must be terminal or browser"));
        if (!TryPlacement(parameters.Placement, out var placement))
        {
            return Task.FromResult(ctx.Fail(
                "invalid_params",
                "placement must be left, right, above, below, or new-shore"));
        }
        if (parameters.Cols <= 0 || parameters.Rows <= 0)
            return Task.FromResult(ctx.Fail("invalid_params", "cols and rows must be positive"));
        var arguments = parameters.Args ?? [];
        if (parameters.NookType == "terminal")
        {
            if (ctx.Nooks is null)
                return Task.FromResult(ctx.Fail("not_ready", "nook registry unavailable"));
            if (!string.IsNullOrEmpty(parameters.Url))
                return Task.FromResult(ctx.Fail("invalid_params", "terminal nooks do not accept a url"));
            if (string.IsNullOrWhiteSpace(parameters.Command)
                && arguments.Length > 0)
            {
                return Task.FromResult(ctx.Fail("invalid_params", "args require a command"));
            }
        }
        else
        {
            if (ctx.Browser is null)
                return Task.FromResult(ctx.Fail("not_ready", "browser manager unavailable"));
            if (!string.IsNullOrWhiteSpace(parameters.Command)
                || arguments.Length > 0)
            {
                return Task.FromResult(ctx.Fail("invalid_params", "browser nooks do not accept a command"));
            }
        }

        var activeBayId = layout.ActiveBayId;
        var relativeNookId = parameters.RelativeToNookId
            ?? ctx.Request.CallerNookId
            ?? layout.FocusedNookFor(activeBayId);
        var relativeLocation = layout.ResolveNookLocation(relativeNookId);
        var bayId = parameters.BayId
            ?? relativeLocation.BayId
            ?? activeBayId;
        string? shoreId;
        if (placement == "new-shore")
        {
            shoreId = null;
        }
        else
        {
            if (string.IsNullOrEmpty(relativeNookId)
                || string.IsNullOrEmpty(relativeLocation.BayId)
                || string.IsNullOrEmpty(relativeLocation.ShoreId))
            {
                return Task.FromResult(ctx.Fail("not_found", "relative nook is not placed"));
            }
            if (!string.Equals(bayId, relativeLocation.BayId, StringComparison.Ordinal))
            {
                return Task.FromResult(ctx.Fail(
                    "invalid_params",
                    "bayId must match the relative nook bay"));
            }
            shoreId = relativeLocation.ShoreId;
        }
        if (string.IsNullOrEmpty(bayId))
            return Task.FromResult(ctx.Fail("not_found", "active bay is unavailable"));

        string? nookId = null;
        var placed = false;
        try
        {
            NookType nookType;
            if (parameters.NookType == "terminal")
            {
                var nook = ctx.Nooks!.Spawn(new SpawnParams(
                    parameters.Command,
                    arguments,
                    parameters.Cwd,
                    Cols: parameters.Cols,
                    Rows: parameters.Rows,
                    Bay: bayId,
                    Shore: shoreId));
                nookId = nook.NookId;
                nookType = NookType.Terminal;
            }
            else
            {
                nookId = "nook-" + Guid.NewGuid().ToString("N");
                ctx.Browser!.Open(
                    nookId,
                    parameters.Url ?? "https://duckduckgo.com");
                nookType = NookType.Browser;
            }
            var leaf = Leaf(nookId, nookType);
            if (placement == "new-shore")
            {
                shoreId = layout.CreateShoreInWing(
                    bayId,
                    LayoutService.MainWingId,
                    parameters.NookType == "terminal" ? "Terminal" : "Browser",
                    leaf);
            }
            else
            {
                layout.SplitNook(
                    shoreId!,
                    relativeNookId!,
                    Orientation(placement),
                    leaf,
                    Before(placement));
            }
            placed = true;
            layout.FocusNook(shoreId!, nookId);
            return Task.FromResult(ctx.Ok(
                new NookOpenResult(
                    nookId,
                    parameters.NookType,
                    bayId,
                    shoreId!,
                    placement),
                Cove.Protocol.CoveJsonContext.Default.NookOpenResult));
        }
        catch (Exception exception)
        {
            if (nookId is not null)
            {
                if (placed)
                    layout.CloseNook(shoreId!, nookId);
                if (parameters.NookType == "terminal")
                    ctx.Nooks!.Kill(nookId);
                else
                    ctx.Browser!.Close(nookId);
            }
            return Task.FromResult(ctx.Fail(
                exception is WorkingDirectoryException ? "invalid_cwd" : "launch_failed",
                exception.Message));
        }
    }

    private static bool TryPlacement(string value, out string placement)
    {
        placement = value;
        return value is "left"
            or "right"
            or "above"
            or "below"
            or "new-shore";
    }

    private static SplitOrientation Orientation(string placement) =>
        placement is "left" or "right"
            ? SplitOrientation.Row
            : SplitOrientation.Column;

    private static bool Before(string placement) =>
        placement is "left" or "above";

    private static NookLeaf Leaf(string nookId, NookType nookType) => new()
    {
        NookId = nookId,
        Subtabs = [new Subtab(nookId, nookType)],
    };
}
