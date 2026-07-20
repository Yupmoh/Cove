using System.Text.Json;
using Cove.Engine.Layout;
using Cove.Engine.Protocol;
using Cove.Engine.Restart;
using Cove.Persistence;
using Cove.Protocol;

namespace Cove.Engine;

internal static class AgentLaunchCommands
{
    [CoveCommand("cove://commands/agent.launch")]
    public static async Task<ControlResponse> Launch(
        EngineDispatchContext ctx)
    {
        if (ctx.Nooks is not { } nooks
            || ctx.Layout is not { } layout
            || ctx.Launcher is not { } launcher)
        {
            return ctx.Fail(
                "not_ready",
                "agent launch services unavailable");
        }
        if (ctx.Request.Params is not JsonElement element
            || element.Deserialize(
                Cove.Protocol.CoveJsonContext.Default.AgentLaunchParams)
                is not { } parameters)
        {
            return ctx.Fail(
                "invalid_params",
                "agent launch params required");
        }
        if (parameters.Mode is not ("new" or "resume"))
        {
            return ctx.Fail(
                "invalid_params",
                "mode must be new or resume");
        }
        if (string.IsNullOrWhiteSpace(parameters.Adapter))
        {
            return ctx.Fail(
                "invalid_params",
                "adapter is required");
        }
        if (!TryScope(parameters.AccessScope, out var accessScope))
        {
            return ctx.Fail(
                "invalid_params",
                "accessScope must be same-tab, same-bay, or all");
        }
        if (!TryPlacement(parameters.Placement, out var placement))
        {
            return ctx.Fail(
                "invalid_params",
                "placement must be left, right, above, below, or new-shore");
        }
        if (parameters.Mode == "resume"
            && string.IsNullOrWhiteSpace(parameters.SessionId))
        {
            return ctx.Fail(
                "invalid_params",
                "resume mode requires sessionId");
        }

        var activeBayId = layout.ActiveBayId;
        var relativeNookId = parameters.RelativeToNookId
            ?? ctx.Request.CallerNookId
            ?? layout.FocusedNookFor(activeBayId);
        var relativeLocation = layout.ResolveNookLocation(
            relativeNookId);
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
                return ctx.Fail(
                    "not_found",
                    "relative nook is not placed");
            }
            if (!string.Equals(
                    bayId,
                    relativeLocation.BayId,
                    StringComparison.Ordinal))
            {
                return ctx.Fail(
                    "invalid_params",
                    "bayId must match the relative nook bay");
            }
            shoreId = relativeLocation.ShoreId;
        }

        var profile = string.IsNullOrWhiteSpace(parameters.Profile)
            ? launcher.ResolveProfile(parameters.Adapter)
            : launcher.FindProfile(parameters.Adapter, parameters.Profile);
        if (profile is null)
        {
            return ctx.Fail(
                "not_found",
                string.IsNullOrWhiteSpace(parameters.Profile)
                    ? $"no launch profile for {parameters.Adapter}"
                    : $"unknown launch profile {parameters.Adapter}/{parameters.Profile}");
        }
        var overrides = new LauncherOverrides
        {
            Yolo = parameters.Yolo,
            WorkingDir = parameters.Cwd,
        };

        ResumeCommand command;
        if (parameters.Mode == "resume")
        {
            var resumed = await launcher.ResumeAsync(
                profile,
                parameters.SessionId!,
                overrides,
                ctx.CancellationToken).ConfigureAwait(false);
            if (resumed.State != AgentResumeState.Succeeded
                || resumed.Command is null)
            {
                return ctx.Fail(
                    "launch_failed",
                    resumed.Nudge?.Message
                        ?? "adapter resume failed");
            }
            command = resumed.Command;
        }
        else
        {
            try
            {
                command = await launcher.BuildLaunchCommandAsync(
                    profile,
                    overrides,
                    ctx.CancellationToken).ConfigureAwait(false);
            }
            catch (ResumeFailedException ex)
            {
                return ctx.Fail("launch_failed", ex.Message);
            }
        }

        NookInfo? nook = null;
        var placed = false;
        try
        {
            nook = nooks.Spawn(
                new SpawnParams(
                    command.Command,
                    command.Args.ToArray(),
                    command.Cwd,
                    new Dictionary<string, string>(profile.Env),
                    parameters.Cols,
                    parameters.Rows,
                    Adapter: parameters.Adapter,
                    AgentName: parameters.Name,
                    Bay: bayId,
                    Shore: shoreId,
                    McpAccessScope: parameters.AccessScope,
                    SessionId: parameters.SessionId,
                    Yolo: parameters.Yolo));
            var leaf = Leaf(nook.NookId);
            if (placement == "new-shore")
            {
                shoreId = layout.CreateShoreInWing(
                    bayId,
                    LayoutService.MainWingId,
                    parameters.Name ?? parameters.Adapter,
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
            ctx.AgentRouter?.Register(
                nook.NookId,
                parameters.Adapter,
                parameters.Name,
                bayId,
                shoreId,
                mcpAccessScope: parameters.AccessScope);
            ctx.Sessions?.Register(
                nook.NookId,
                parameters.Adapter,
                parameters.SessionId);
            launcher.PersistOverrides(nook.NookId, overrides);
            ctx.NookScopes?.SetScope(nook.NookId, accessScope);
            ctx.RecentSessions?.RecordStart(
                parameters.Adapter,
                parameters.SessionId ?? nook.NookId,
                bayId,
                command.Cwd,
                DateTimeOffset.UtcNow);
            ctx.HookRouter?.Seed(
                nook.NookId,
                parameters.Adapter,
                parameters.SessionId);
            return ctx.Ok(
                new AgentLaunchResult(
                    nook.NookId,
                    parameters.Adapter,
                    parameters.SessionId,
                    bayId,
                    shoreId!,
                    placement,
                    parameters.Mode == "resume"),
                Cove.Protocol.CoveJsonContext.Default.AgentLaunchResult);
        }
        catch (Exception ex)
        {
            if (nook is not null)
            {
                ctx.AgentRouter?.Unregister(nook.NookId);
                ctx.Sessions?.Unregister(nook.NookId);
                launcher.ClearOverrides(nook.NookId);
                ctx.NookScopes?.ClearScope(nook.NookId);
                if (placed)
                    layout.CloseNook(shoreId!, nook.NookId);
                nooks.Kill(nook.NookId);
            }
            return ctx.Fail("launch_failed", ex.Message);
        }
    }

    private static bool TryScope(
        string value,
        out McpScope scope)
    {
        scope = value switch
        {
            "same-tab" => McpScope.SameTab,
            "same-bay" => McpScope.SameBay,
            "all" => McpScope.All,
            _ => (McpScope)(-1),
        };
        return (int)scope >= 0;
    }

    private static bool TryPlacement(
        string value,
        out string placement)
    {
        placement = value;
        return value is "left"
            or "right"
            or "above"
            or "below"
            or "new-shore";
    }

    private static SplitOrientation Orientation(
        string placement) =>
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
