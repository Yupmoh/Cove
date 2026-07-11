using System.Text.Json;
using Cove.Engine.Protocol;
using Cove.Generated;
using Cove.Protocol;
namespace Cove.Engine.ProtocolDispatch;

public static class ProtocolCommands
{
    [CoveCommand("cove://commands/protocol.resolve")]
    public static Task<ControlResponse> Resolve(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ProtocolResolveParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "protocol resolve params required"));

        var resolver = new ProtocolResolver();
        var (uri, resolvedParams) = resolver.Resolve(p.Uri, p.FocusedNookId, p.ActiveShoreId);
        if (uri is null)
            return Task.FromResult(ctx.Fail("not_found", $"unresolvable cove:// URI: {p.Uri}"));

        return Task.FromResult(ctx.Ok(new ProtocolResolveResult(uri, resolvedParams), CoveJsonContext.Default.ProtocolResolveResult));
    }
}
