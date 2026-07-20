using System.Text.Json;
using Cove.Engine.Layout;
using Cove.Persistence;
using Cove.Protocol;

namespace Cove.Engine;

internal static class NookOpenCommands
{
    [CoveCommand("cove://commands/nook.open")]
    public static Task<ControlResponse> Open(EngineDispatchContext ctx)
    {
        if (ctx.Nooks is not { } nooks)
            return Task.FromResult(ctx.Fail("not_ready", "nook registry unavailable"));
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail("not_ready", "layout service unavailable"));
        if (ctx.Request.Params is not JsonElement element
            || element.Deserialize(Cove.Protocol.CoveJsonContext.Default.NookOpenParams) is not { } parameters)
        {
            return Task.FromResult(ctx.Fail("invalid_params", "nook open params required"));
        }
        if (parameters.NookType != "terminal")
            return Task.FromResult(ctx.Fail("invalid_params", "nook type must be terminal"));
        if (!TryPlacement(parameters.Placement, out var placement))
        {
            return Task.FromResult(ctx.Fail(
                "invalid_params",
                "placement must be left, right, above, below, or new-shore"));
        }
        if (parameters.Cols <= 0 || parameters.Rows <= 0)
            return Task.FromResult(ctx.Fail("invalid_params", "cols and rows must be positive"));
        var arguments = parameters.Args ?? [];
        if (string.IsNullOrWhiteSpace(parameters.Command)
            && arguments.Length > 0)
        {
            return Task.FromResult(ctx.Fail("invalid_params", "args require a command"));
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

        NookInfo? nook = null;
        var placed = false;
        try
        {
            nook = nooks.Spawn(new SpawnParams(
                parameters.Command,
                arguments,
                parameters.Cwd,
                Cols: parameters.Cols,
                Rows: parameters.Rows,
                Bay: bayId,
                Shore: shoreId));
            var leaf = Leaf(nook.NookId);
            if (placement == "new-shore")
            {
                shoreId = layout.CreateShoreInWing(
                    bayId,
                    LayoutService.MainWingId,
                    "Terminal",
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
            layout.FocusNook(shoreId!, nook.NookId);
            return Task.FromResult(ctx.Ok(
                new NookOpenResult(
                    nook.NookId,
                    "terminal",
                    bayId,
                    shoreId!,
                    placement),
                Cove.Protocol.CoveJsonContext.Default.NookOpenResult));
        }
        catch (Exception exception)
        {
            if (nook is not null)
            {
                if (placed)
                    layout.CloseNook(shoreId!, nook.NookId);
                nooks.Kill(nook.NookId);
            }
            return Task.FromResult(ctx.Fail("launch_failed", exception.Message));
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

    private static NookLeaf Leaf(string nookId) => new()
    {
        NookId = nookId,
        Subtabs = [new Subtab(nookId, NookType.Terminal)],
    };
}
