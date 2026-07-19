using System.Text.Json;
using Cove.Adapters;
using Cove.Protocol;

namespace Cove.Engine.Hooks;

internal static class HookCommands
{
    [CoveCommand("cove://commands/hook.emit")]
    public static Task<ControlResponse> Emit(EngineDispatchContext ctx)
    {
        if (ctx.HookRouter is not { } router)
            return Task.FromResult(ctx.Fail("not_ready", "hook router unavailable"));
        if (ctx.Request.Params is not JsonElement element
            || element.Deserialize(CoveJsonContext.Default.HookEmitParams) is not { } parameters
            || string.IsNullOrWhiteSpace(parameters.Adapter)
            || string.IsNullOrWhiteSpace(parameters.Event))
        {
            return Task.FromResult(ctx.Fail(
                "invalid_params",
                "adapter and event are required"));
        }

        var nookId = parameters.NookId;
        if (!string.IsNullOrEmpty(ctx.Request.CallerNookId))
        {
            if (!string.IsNullOrEmpty(nookId)
                && !string.Equals(
                    nookId,
                    ctx.Request.CallerNookId,
                    StringComparison.Ordinal))
            {
                return Task.FromResult(ctx.Fail(
                    "access_denied",
                    "hook event targets another nook"));
            }
            nookId = ctx.Request.CallerNookId;
        }

        router.Route(new HookEvent
        {
            Adapter = parameters.Adapter,
            Event = parameters.Event,
            NookId = nookId,
            Payload = parameters.Payload.ValueKind == JsonValueKind.Undefined
                ? null
                : parameters.Payload.Clone()
        });
        return Task.FromResult(ctx.OkJson("{}"));
    }
}
