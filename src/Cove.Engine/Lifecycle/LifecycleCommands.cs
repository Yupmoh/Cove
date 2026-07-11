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
        if (ctx.Nooks is not { } nooks)
            return Task.FromResult(ctx.Fail("not_ready", "nook registry not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NookRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "nook ref required"));

        ctrl.Stop(p.NookId);
        nooks.Stop(p.NookId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/agent.close")]
    public static Task<ControlResponse> Close(EngineDispatchContext ctx)
    {
        if (ctx.Lifecycle is not { } ctrl)
            return Task.FromResult(ctx.Fail("not_ready", "lifecycle controller not available"));
        if (ctx.Nooks is not { } nooks)
            return Task.FromResult(ctx.Fail("not_ready", "nook registry not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NookRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "nook ref required"));

        ctrl.Close(p.NookId);
        nooks.Kill(p.NookId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/agent.replay")]
    public static Task<ControlResponse> Replay(EngineDispatchContext ctx)
    {
        if (ctx.Lifecycle is not { } ctrl)
            return Task.FromResult(ctx.Fail("not_ready", "lifecycle controller not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NookRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "nook ref required"));

        var info = ctrl.GetReplayInfo(p.NookId);
        if (info is null)
            return Task.FromResult(ctx.Fail("not_found", "no replay info for nook"));
        return Task.FromResult(ctx.Ok(new ReplayInfoDto(info.Command, info.ExitCode, info.Signal), CoveJsonContext.Default.ReplayInfoDto));
    }

    [CoveCommand("cove://commands/agent.spawned-nooks")]
    public static Task<ControlResponse> SpawnedNooks(EngineDispatchContext ctx)
    {
        if (ctx.Lifecycle is not { } ctrl)
            return Task.FromResult(ctx.Fail("not_ready", "lifecycle controller not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NookRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "nook ref required"));

        var nooks = ctrl.GetSpawnedNooks(p.NookId);
        return Task.FromResult(ctx.Ok(new SpawnedNooksResult(nooks), CoveJsonContext.Default.SpawnedNooksResult));
    }
}
