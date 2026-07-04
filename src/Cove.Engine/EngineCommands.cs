using System;
using System.Threading.Tasks;
using Cove.Protocol;

namespace Cove.Engine;

internal static class EngineCommands
{
    [CoveCommand("cove://commands/pane.list")]
    public static Task<ControlResponse> PaneList(EngineDispatchContext ctx)
        => Task.FromResult(ctx.Ok(new PaneListResult(Array.Empty<PaneInfo>()), CoveJsonContext.Default.PaneListResult));
}
