using Cove.Protocol;

namespace Cove.Engine;

internal static class HookPaneStateCommands
{
    [CoveCommand("cove://hooks/pane-states")]
    public static Task<ControlResponse> PaneStates(EngineDispatchContext ctx)
    {
        if (ctx.HookRouter is not { } router)
            return Task.FromResult(ctx.Fail("not_ready", "hook router unavailable"));
        var states = router.GetAllPaneStates().Values
            .Select(s => new PaneStateItem(s.PaneId, s.Adapter, s.Status, s.ActiveSubagents, s.LastEventAt))
            .ToArray();
        return Task.FromResult(ctx.Ok(new PaneStatesResult(states), CoveJsonContext.Default.PaneStatesResult));
    }
}
