using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Sessions;

public static class SessionCommands
{
    [CoveCommand("cove://commands/session.dismiss")]
    public static Task<ControlResponse> Dismiss(EngineDispatchContext ctx)
    {
        if (ctx.Sessions is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "session orchestrator not available"));
        if (ctx.Panes is not { } panes)
            return Task.FromResult(ctx.Fail("not_ready", "pane registry not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.PaneRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "pane ref required"));

        orch.Dismiss(p.PaneId);
        panes.Kill(p.PaneId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/session.background")]
    public static Task<ControlResponse> Background(EngineDispatchContext ctx)
    {
        if (ctx.Sessions is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "session orchestrator not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.PaneRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "pane ref required"));

        orch.Background(p.PaneId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/session.foreground")]
    public static Task<ControlResponse> Foreground(EngineDispatchContext ctx)
    {
        if (ctx.Sessions is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "session orchestrator not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.PaneRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "pane ref required"));

        orch.Foreground(p.PaneId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/session.stop")]
    public static Task<ControlResponse> Stop(EngineDispatchContext ctx)
    {
        if (ctx.Sessions is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "session orchestrator not available"));
        if (ctx.Panes is not { } panes)
            return Task.FromResult(ctx.Fail("not_ready", "pane registry not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.PaneRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "pane ref required"));

        orch.Stop(p.PaneId);
        panes.Kill(p.PaneId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/session.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.Sessions is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "session orchestrator not available"));

        var dismissed = orch.ListDismissed().Select(s => new SessionStateDto(s.PaneId, s.Adapter, s.SessionId, s.Lifecycle.ToString().ToLowerInvariant(), s.Resumable)).ToList();
        var background = orch.ListBackground().Select(s => new SessionStateDto(s.PaneId, s.Adapter, s.SessionId, s.Lifecycle.ToString().ToLowerInvariant(), s.Resumable)).ToList();
        var all = dismissed.Concat(background).ToList();
        return Task.FromResult(ctx.Ok(new SessionListResult(all), CoveJsonContext.Default.SessionListResult));
    }
}
