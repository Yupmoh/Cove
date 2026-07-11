using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Nooks;

public static class NookTypeCommands
{
    [CoveCommand("cove://commands/nook-types.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.NookTypes is not { } registry)
            return Task.FromResult(ctx.Fail("not_ready", "nook type registry not available"));

        var items = registry.List().Select(t => new NookTypeDto(t.Name, t.DisplayName, t.ContentSource, t.IsDockable)).ToList();
        return Task.FromResult(ctx.Ok(new NookTypeListResult(items), CoveJsonContext.Default.NookTypeListResult));
    }
}
