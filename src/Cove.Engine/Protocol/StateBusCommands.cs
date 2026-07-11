using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Protocol;

public static class StateBusCommands
{
    [CoveCommand("cove://commands/state.read")]
    public static Task<ControlResponse> Read(EngineDispatchContext ctx)
    {
        if (ctx.StateBus is not { } bus)
            return Task.FromResult(ctx.Fail("not_ready", "state bus unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.StateReadParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "scope, namespace, and id required"));

        var (exists, value) = bus.Read(p.Scope, p.Namespace, p.Id);
        return Task.FromResult(ctx.Ok(new StateReadResult(exists, value), CoveJsonContext.Default.StateReadResult));
    }

    [CoveCommand("cove://commands/state.write")]
    public static Task<ControlResponse> Write(EngineDispatchContext ctx)
    {
        if (ctx.StateBus is not { } bus)
            return Task.FromResult(ctx.Fail("not_ready", "state bus unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.StateWriteParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "state write params required"));

        if (!StateBus.IsValidScope(p.Scope))
            return Task.FromResult(ctx.Fail("invalid_params", "scope must be app, bay, tab, or nook"));

        bus.Write(p.Scope, p.Namespace, p.Id, p.Value);
        return Task.FromResult(ctx.Ok());
    }
}
