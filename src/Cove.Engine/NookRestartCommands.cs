using System.Text.Json;
using Cove.Engine.Agents;
using Cove.Engine.Hooks;
using Cove.Engine.Restart;
using Cove.Engine.Sessions;
using Cove.Protocol;

namespace Cove.Engine;

internal static class NookRestartCommands
{
    [CoveCommand("cove://commands/nook.restart")]
    public static async Task<ControlResponse> Restart(
        EngineDispatchContext ctx)
    {
        if (ctx.Nooks is not { } nooks)
        {
            return ctx.Fail(
                "not_ready",
                "nook registry unavailable");
        }
        if (ctx.Request.Params is not JsonElement element
            || element.Deserialize(
                CoveJsonContext.Default.NookRestartParams)
                is not { } parameters)
        {
            return ctx.Fail(
                "invalid_params",
                "nook restart params required");
        }
        if (parameters.Mode
            is not ("fresh" or "resume-current" or "command"))
        {
            return ctx.Fail(
                "invalid_params",
                "mode must be fresh, resume-current, or command");
        }
        if (parameters.ResumeFallback is not ("none" or "fresh"))
        {
            return ctx.Fail(
                "invalid_params",
                "resumeFallback must be none or fresh");
        }
        var resolved = nooks.ResolveId(parameters.NookId);
        if (!resolved.Found)
        {
            return ctx.Fail(
                resolved.ErrorCode ?? "not_found",
                $"unknown nook {parameters.NookId}");
        }
        var nookId = resolved.Id!;
        var descriptor = nooks.Descriptors()
            .First(candidate => candidate.NookId == nookId);
        var agent = ctx.AgentRouter?.ResolveTarget(nookId);
        var session = ctx.Sessions?.GetState(nookId);
        var adapter = agent?.Adapter ?? descriptor.Adapter;
        var agentName = agent?.Name ?? descriptor.AgentName;
        var priorOverrides = ctx.Launcher?.GetOverrides(nookId);
        var overrides = priorOverrides ?? new LauncherOverrides
        {
            WorkingDir = descriptor.Cwd,
        };
        var restoreState = parameters.PreserveScrollback
            ? nooks.CaptureTerminalRestoreState(nookId)
            : null;
        var priorScrollback = parameters.PreserveScrollback
            && restoreState is null
            ? nooks.SnapshotRing(nookId)
            : null;
        var oldHookState = ctx.HookRouter?.GetNookState(nookId);
        var location = ctx.Layout?.ResolveNookLocation(nookId);

        ResumeCommand command;
        IReadOnlyDictionary<string, string>? environment = null;
        string? nextSessionId = null;
        var fallbackUsed = false;
        if (parameters.Mode == "resume-current")
        {
            if (string.IsNullOrEmpty(adapter)
                || string.IsNullOrEmpty(session?.SessionId)
                || ctx.Launcher is not { } launcher)
            {
                return ctx.Fail(
                    "not_ready",
                    "current adapter session is not resumable");
            }
            var profile = launcher.FindProfile(adapter, "default");
            if (profile is null)
            {
                return ctx.Fail(
                    "not_found",
                    $"unknown launch profile {adapter}/default");
            }
            var resumed = await launcher.ResumeAsync(
                profile,
                session.SessionId,
                overrides,
                ctx.CancellationToken).ConfigureAwait(false);
            if (resumed.State != AgentResumeState.Succeeded
                || resumed.Command is null)
            {
                if (parameters.ResumeFallback != "fresh")
                {
                    return ctx.Fail(
                        "resume_failed",
                        resumed.Nudge?.Message
                            ?? "adapter resume failed");
                }
                try
                {
                    command = await launcher
                        .BuildLaunchCommandAsync(
                            profile,
                            overrides,
                            ctx.CancellationToken)
                        .ConfigureAwait(false);
                }
                catch (ResumeFailedException ex)
                {
                    return ctx.Fail(
                        "restart_failed",
                        ex.Message);
                }
                environment = profile.Env;
                fallbackUsed = true;
            }
            else
            {
                command = resumed.Command;
                environment = profile.Env;
                nextSessionId = session.SessionId;
            }
        }
        else if (parameters.Mode == "command")
        {
            if (ctx.Request.CallerNookId is not null)
            {
                return ctx.Fail(
                    "forbidden",
                    "command restart requires a control caller");
            }
            if (!string.IsNullOrEmpty(adapter))
            {
                return ctx.Fail(
                    "invalid_target",
                    "command restart is limited to terminal nooks");
            }
            if (string.IsNullOrWhiteSpace(parameters.Command))
            {
                return ctx.Fail(
                    "invalid_params",
                    "command mode requires command");
            }
            command = new ResumeCommand(
                parameters.Command,
                parameters.Args ?? [],
                parameters.Cwd ?? descriptor.Cwd);
        }
        else if (!string.IsNullOrEmpty(adapter))
        {
            if (ctx.Launcher is not { } launcher)
            {
                return ctx.Fail(
                    "not_ready",
                    "agent launcher unavailable");
            }
            var profile = launcher.FindProfile(adapter, "default");
            if (profile is null)
            {
                return ctx.Fail(
                    "not_found",
                    $"unknown launch profile {adapter}/default");
            }
            try
            {
                command = await launcher.BuildLaunchCommandAsync(
                    profile,
                    overrides,
                    ctx.CancellationToken).ConfigureAwait(false);
            }
            catch (ResumeFailedException ex)
            {
                return ctx.Fail("restart_failed", ex.Message);
            }
            environment = profile.Env;
        }
        else
        {
            command = new ResumeCommand(
                descriptor.Command,
                descriptor.Args,
                descriptor.Cwd);
        }

        var replaced = false;
        try
        {
            Respawn(
                nooks,
                descriptor,
                command,
                restoreState,
                priorScrollback,
                adapter,
                agentName,
                environment);
            replaced = true;
            RestoreTitle(nooks, descriptor);
            RestoreMetadata(
                ctx,
                nookId,
                adapter,
                agentName,
                nextSessionId,
                agent,
                overrides);
            return ctx.Ok(
                new NookRestartResult(
                    nookId,
                    parameters.Mode,
                    fallbackUsed
                        ? "fallback-fresh"
                        : parameters.Mode switch
                        {
                            "resume-current" => "resumed",
                            "command" => "command",
                            _ => "fresh",
                        },
                    fallbackUsed,
                    adapter,
                    nextSessionId,
                    location?.BayId,
                    location?.ShoreId,
                    restoreState?.Tail.Length
                        ?? priorScrollback?.Length
                        ?? 0),
                CoveJsonContext.Default.NookRestartResult);
        }
        catch (Exception ex)
        {
            if (replaced)
            {
                try
                {
                    Respawn(
                        nooks,
                        descriptor,
                        new ResumeCommand(
                            descriptor.Command,
                            descriptor.Args,
                            descriptor.Cwd),
                        restoreState,
                        priorScrollback,
                        descriptor.Adapter,
                        descriptor.AgentName,
                        null);
                    RestoreTitle(nooks, descriptor);
                    RestorePriorMetadata(
                        ctx,
                        nookId,
                        agent,
                        session,
                        priorOverrides,
                        oldHookState);
                }
                catch (Exception rollback)
                {
                    return ctx.Fail(
                        "rollback_failed",
                        $"{ex.Message}; rollback failed: {rollback.Message}");
                }
            }
            return ctx.Fail("restart_failed", ex.Message);
        }
    }

    private static void Respawn(
        Pty.NookRegistry nooks,
        Persistence.NookDescriptor descriptor,
        ResumeCommand command,
        Pty.TerminalRestoreState? restoreState,
        byte[]? priorScrollback,
        string? adapter,
        string? agentName,
        IReadOnlyDictionary<string, string>? environment)
    {
        if (restoreState is not null)
        {
            nooks.RespawnAs(
                descriptor.NookId,
                command.Command,
                command.Args.ToArray(),
                command.Cwd,
                descriptor.Cols,
                descriptor.Rows,
                restoreState,
                adapter,
                agentName,
                environment);
            return;
        }
        nooks.RespawnAs(
            descriptor.NookId,
            command.Command,
            command.Args.ToArray(),
            command.Cwd,
            descriptor.Cols,
            descriptor.Rows,
            priorScrollback,
            adapter,
            agentName,
            environment);
    }

    private static void RestoreTitle(
        Pty.NookRegistry nooks,
        Persistence.NookDescriptor descriptor)
    {
        if (!string.IsNullOrEmpty(descriptor.Title))
            nooks.Rename(descriptor.NookId, descriptor.Title);
    }

    private static void RestoreMetadata(
        EngineDispatchContext ctx,
        string nookId,
        string? adapter,
        string? agentName,
        string? sessionId,
        AgentInfo? priorAgent,
        LauncherOverrides overrides)
    {
        if (string.IsNullOrEmpty(adapter))
            return;
        var location = ctx.Layout?.ResolveNookLocation(nookId);
        ctx.AgentRouter?.Register(
            nookId,
            adapter,
            agentName,
            location?.BayId,
            location?.ShoreId,
            status: "idle",
            mcpVisible: priorAgent?.McpVisible ?? true,
            mcpAccessScope:
                priorAgent?.McpAccessScope ?? "same-bay");
        ctx.Sessions?.Register(nookId, adapter, sessionId);
        ctx.Launcher?.PersistOverrides(nookId, overrides);
        ctx.HookRouter?.Reset(
            nookId,
            adapter,
            sessionId);
    }

    private static void RestorePriorMetadata(
        EngineDispatchContext ctx,
        string nookId,
        AgentInfo? agent,
        SessionState? session,
        LauncherOverrides? overrides,
        NookAgentState? hookState)
    {
        if (agent is not null)
        {
            ctx.AgentRouter?.Register(
                agent.NookId,
                agent.Adapter,
                agent.Name,
                agent.Bay,
                agent.Shore,
                agent.Status,
                agent.McpVisible,
                agent.McpAccessScope);
        }
        if (session is not null)
        {
            ctx.Sessions?.Register(
                nookId,
                session.Adapter,
                session.SessionId);
            RestoreLifecycle(ctx.Sessions, nookId, session.Lifecycle);
        }
        if (overrides is null)
            ctx.Launcher?.ClearOverrides(nookId);
        else
            ctx.Launcher?.PersistOverrides(nookId, overrides);
        if (hookState is not null)
        {
            ctx.HookRouter?.Reset(
                nookId,
                hookState.Adapter,
                hookState.SessionId,
                hookState.Status);
        }
    }

    private static void RestoreLifecycle(
        SessionResumeOrchestrator? sessions,
        string nookId,
        SessionLifecycle lifecycle)
    {
        if (sessions is null)
            return;
        switch (lifecycle)
        {
            case SessionLifecycle.Dismissed:
                sessions.Dismiss(nookId);
                break;
            case SessionLifecycle.Background:
                sessions.Background(nookId);
                break;
            case SessionLifecycle.Waking:
                sessions.MarkWaking(nookId);
                break;
            case SessionLifecycle.Cancelled:
                sessions.Stop(nookId);
                break;
        }
    }
}
