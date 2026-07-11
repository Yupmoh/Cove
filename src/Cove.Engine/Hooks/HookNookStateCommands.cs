using Cove.Protocol;

namespace Cove.Engine;

internal static class HookNookStateCommands
{
    [CoveCommand("cove://hooks/nook-states")]
    public static Task<ControlResponse> NookStates(EngineDispatchContext ctx)
    {
        if (ctx.HookRouter is not { } router)
            return Task.FromResult(ctx.Fail("not_ready", "hook router unavailable"));
        var states = router.GetAllNookStates().Values
            .Select(s => new NookStateItem(s.NookId, s.Adapter, s.Status, s.ActiveSubagents, s.LastEventAt))
            .ToArray();
        return Task.FromResult(ctx.Ok(new NookStatesResult(states), CoveJsonContext.Default.NookStatesResult));
    }
}
