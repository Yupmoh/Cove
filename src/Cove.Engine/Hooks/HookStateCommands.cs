using System.Text.Json;
using System.Threading.Tasks;
using Cove.Engine.Hooks;
using Cove.Protocol;

namespace Cove.Engine;

internal static class HookStateCommands
{
    [CoveCommand("cove://hooks/_state")]
    public static Task<ControlResponse> State(EngineDispatchContext ctx)
    {
        if (ctx.HookServer is not { } server)
            return Task.FromResult(ctx.Fail("not_ready", "hook server unavailable"));
        return Task.FromResult(ctx.Ok(new HookStateResult(server.Port, server.IsRunning), CoveJsonContext.Default.HookStateResult));
    }
}

