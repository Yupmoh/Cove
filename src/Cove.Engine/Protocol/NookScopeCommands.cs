using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Protocol;

public static class NookScopeCommands
{
    [CoveCommand("cove://commands/nook.scope.get")]
    public static Task<ControlResponse> GetScope(EngineDispatchContext ctx)
    {
        if (ctx.NookScopes is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "nook scope store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NookScopeGetParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "nook scope get params required"));

        var scope = store.GetScope(p.NookId);
        return Task.FromResult(ctx.Ok(new NookScopeResult(p.NookId, ScopeToString(scope)), CoveJsonContext.Default.NookScopeResult));
    }

    [CoveCommand("cove://commands/nook.scope.set")]
    public static Task<ControlResponse> SetScope(EngineDispatchContext ctx)
    {
        if (ctx.NookScopes is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "nook scope store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NookScopeSetParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "nook scope set params required"));

        if (!TryParseScope(p.Scope, out var scope))
            return Task.FromResult(ctx.Fail("invalid_params", $"scope must be same-tab, same-bay, or all"));

        store.SetScope(p.NookId, scope);
        return Task.FromResult(ctx.Ok());
    }

    private static bool TryParseScope(string value, out McpScope scope)
    {
        switch (value.ToLowerInvariant())
        {
            case "same-tab": scope = McpScope.SameTab; return true;
            case "same-bay": scope = McpScope.SameBay; return true;
            case "all": scope = McpScope.All; return true;
            default: scope = McpScope.SameBay; return false;
        }
    }

    private static string ScopeToString(McpScope scope) => scope switch
    {
        McpScope.SameTab => "same-tab",
        McpScope.SameBay => "same-bay",
        McpScope.All => "all",
        _ => "same-bay",
    };
}
