using System.Text.Json;
using Cove.Engine.Protocol;
using Cove.Protocol;

namespace Cove.Engine;

internal static class WorkspaceCommands
{
    [CoveCommand("cove://commands/workspace.context")]
    public static Task<ControlResponse> Context(EngineDispatchContext ctx)
    {
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail(
                "not_ready",
                "layout service unavailable"));

        var requestedNookId = ctx.Request.Params is JsonElement element
            ? element.Deserialize(
                CoveJsonContext.Default.WorkspaceContextParams)?.NookId
            : null;
        var activeBayId = layout.ActiveBayId;
        var focusedNookId = layout.FocusedNookFor(activeBayId);
        var nookId = requestedNookId
            ?? ctx.Request.CallerNookId
            ?? focusedNookId;
        if (string.IsNullOrEmpty(nookId))
        {
            return Task.FromResult(ctx.Fail(
                "not_found",
                "workspace has no target nook"));
        }

        var location = layout.ResolveNookLocation(nookId);
        var agent = ctx.AgentRouter?.ResolveTarget(nookId);
        var session = ctx.Sessions?.GetState(nookId);
        var nook = ctx.Nooks?
            .List()
            .FirstOrDefault(candidate => candidate.NookId == nookId);
        var descriptor = ctx.Nooks?
            .Descriptors()
            .FirstOrDefault(candidate => candidate.NookId == nookId);
        var scope = ctx.NookScopes?.GetScope(nookId)
            ?? McpScope.SameBay;
        var result = new WorkspaceContextResult(
            nookId,
            agent?.Adapter,
            session?.SessionId,
            location.BayId,
            location.ShoreId,
            focusedNookId,
            activeBayId,
            layout.ActiveShoreFor(activeBayId),
            ctx.GetWorkspaceRevision?.Invoke() ?? 0,
            nook?.Cwd ?? descriptor?.Cwd,
            ScopeName(scope));
        return Task.FromResult(ctx.Ok(
            result,
            CoveJsonContext.Default.WorkspaceContextResult));
    }

    private static string ScopeName(McpScope scope) => scope switch
    {
        McpScope.SameTab => "same-tab",
        McpScope.SameBay => "same-bay",
        McpScope.All => "all",
        _ => "same-bay",
    };
}
