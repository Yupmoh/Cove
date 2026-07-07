using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Protocol;

public static class PaneScopeCommands
{
    [CoveCommand("cove://commands/pane.scope.get")]
    public static Task<ControlResponse> GetScope(EngineDispatchContext ctx)
    {
        if (ctx.PaneScopes is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "pane scope store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.PaneScopeGetParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "pane scope get params required"));

        var scope = store.GetScope(p.PaneId);
        return Task.FromResult(ctx.Ok(new PaneScopeResult(p.PaneId, ScopeToString(scope)), CoveJsonContext.Default.PaneScopeResult));
    }

    [CoveCommand("cove://commands/pane.scope.set")]
    public static Task<ControlResponse> SetScope(EngineDispatchContext ctx)
    {
        if (ctx.PaneScopes is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "pane scope store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.PaneScopeSetParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "pane scope set params required"));

        if (!TryParseScope(p.Scope, out var scope))
            return Task.FromResult(ctx.Fail("invalid_params", $"scope must be same-tab, same-workspace, or all"));

        store.SetScope(p.PaneId, scope);
        return Task.FromResult(ctx.Ok());
    }

    private static bool TryParseScope(string value, out McpScope scope)
    {
        switch (value.ToLowerInvariant())
        {
            case "same-tab": scope = McpScope.SameTab; return true;
            case "same-workspace": scope = McpScope.SameWorkspace; return true;
            case "all": scope = McpScope.All; return true;
            default: scope = McpScope.SameWorkspace; return false;
        }
    }

    private static string ScopeToString(McpScope scope) => scope switch
    {
        McpScope.SameTab => "same-tab",
        McpScope.SameWorkspace => "same-workspace",
        McpScope.All => "all",
        _ => "same-workspace",
    };
}
