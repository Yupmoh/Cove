using System.Text.Json;
using Cove.Persistence;
using Cove.Protocol;

namespace Cove.Engine;

internal static class NookStackCommands
{
    [CoveCommand("cove://commands/nook.stack")]
    public static Task<ControlResponse> Stack(EngineDispatchContext ctx)
    {
        if (ctx.Layout is not { } layout)
        {
            return Task.FromResult(ctx.Fail(
                "not_ready",
                "layout service unavailable"));
        }
        if (ctx.Request.Params is not JsonElement element
            || element.Deserialize(
                Cove.Protocol.CoveJsonContext.Default.NookStackParams)
            is not { } parameters)
        {
            return Task.FromResult(ctx.Fail(
                "invalid_params",
                "nook stack params required"));
        }
        if (!TryOrientation(parameters.Placement, out var orientation))
        {
            return Task.FromResult(ctx.Fail(
                "invalid_params",
                "placement must be left, right, above, or below"));
        }
        var location = layout.ResolveNookLocation(parameters.NookId);
        if (location.BayId is null || location.ShoreId is null)
        {
            return Task.FromResult(ctx.Fail(
                "not_found",
                $"unknown or unplaced nook {parameters.NookId}"));
        }
        try
        {
            var nooks = layout.BalanceStack(
                location.ShoreId,
                parameters.NookId,
                orientation);
            return Task.FromResult(ctx.Ok(
                new NookStackResult(
                    parameters.NookId,
                    location.BayId,
                    location.ShoreId,
                    parameters.Placement,
                    nooks),
                Cove.Protocol.CoveJsonContext.Default.NookStackResult));
        }
        catch (InvalidOperationException exception)
        {
            return Task.FromResult(ctx.Fail(
                "invalid_state",
                exception.Message));
        }
    }

    private static bool TryOrientation(
        string placement,
        out SplitOrientation orientation)
    {
        if (placement is "left" or "right")
        {
            orientation = SplitOrientation.Row;
            return true;
        }
        if (placement is "above" or "below")
        {
            orientation = SplitOrientation.Column;
            return true;
        }
        orientation = default;
        return false;
    }
}
