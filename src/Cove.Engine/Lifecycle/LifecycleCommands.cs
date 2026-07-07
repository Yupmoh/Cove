using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Lifecycle;

public static class LifecycleCommands
{
    [CoveCommand("cove://commands/agent.stop")]
    public static Task<ControlResponse> Stop(EngineDispatchContext ctx)
    {
        if (ctx.Lifecycle is not { } ctrl)
            return Task.FromResult(ctx.Fail("not_ready", "lifecycle controller not available"));
        if (ctx.Panes is not { } panes)
            return Task.FromResult(ctx.Fail("not_ready", "pane registry not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.PaneRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "pane ref required"));

        ctrl.Stop(p.PaneId);
        panes.Stop(p.PaneId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/agent.close")]
    public static Task<ControlResponse> Close(EngineDispatchContext ctx)
    {
        if (ctx.Lifecycle is not { } ctrl)
            return Task.FromResult(ctx.Fail("not_ready", "lifecycle controller not available"));
        if (ctx.Panes is not { } panes)
            return Task.FromResult(ctx.Fail("not_ready", "pane registry not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.PaneRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "pane ref required"));

        ctrl.Close(p.PaneId);
        panes.Kill(p.PaneId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/agent.replay")]
    public static Task<ControlResponse> Replay(EngineDispatchContext ctx)
    {
        if (ctx.Lifecycle is not { } ctrl)
            return Task.FromResult(ctx.Fail("not_ready", "lifecycle controller not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.PaneRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "pane ref required"));

        var info = ctrl.GetReplayInfo(p.PaneId);
        if (info is null)
            return Task.FromResult(ctx.Fail("not_found", "no replay info for pane"));
        return Task.FromResult(ctx.Ok(new ReplayInfoDto(info.Command, info.ExitCode, info.Signal), CoveJsonContext.Default.ReplayInfoDto));
    }

    [CoveCommand("cove://commands/agent.spawned-panes")]
    public static Task<ControlResponse> SpawnedPanes(EngineDispatchContext ctx)
    {
        if (ctx.Lifecycle is not { } ctrl)
            return Task.FromResult(ctx.Fail("not_ready", "lifecycle controller not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.PaneRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "pane ref required"));

        var panes = ctrl.GetSpawnedPanes(p.PaneId);
        return Task.FromResult(ctx.Ok(new SpawnedPanesResult(panes), CoveJsonContext.Default.SpawnedPanesResult));
    }
}
