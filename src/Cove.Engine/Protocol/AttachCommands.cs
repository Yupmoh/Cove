using System.Text.Json;
using System.Threading.Tasks;
using Cove.Protocol;

namespace Cove.Engine.Protocol;

internal static class AttachCommands
{
    [CoveCommand("cove://commands/attach.raw", Description = "raw external-terminal attach (TM-79, runtime-owned)")]
    public static Task<ControlResponse> RawAttach(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.AttachRawParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "attach raw params required"));
        return Task.FromResult(ctx.Fail("not_implemented", "raw attach is owned by the runtime milestone (TM-79)"));
    }
}
