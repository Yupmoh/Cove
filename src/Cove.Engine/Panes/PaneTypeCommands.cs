using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Panes;

public static class PaneTypeCommands
{
    [CoveCommand("cove://commands/pane-types.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.PaneTypes is not { } registry)
            return Task.FromResult(ctx.Fail("not_ready", "pane type registry not available"));

        var items = registry.List().Select(t => new PaneTypeDto(t.Name, t.DisplayName, t.ContentSource, t.IsDockable)).ToList();
        return Task.FromResult(ctx.Ok(new PaneTypeListResult(items), CoveJsonContext.Default.PaneTypeListResult));
    }
}
