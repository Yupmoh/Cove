using System.Text.Json;
using Cove.Engine.Layout;
using Cove.Engine.Workspaces;
using Cove.Protocol;

namespace Cove.Engine.Protocol;

internal static class ScopeEnforcement
{
    public static bool IsPaneTargetingVerb(string uri)
    {
        return uri.StartsWith("cove://commands/pane.write", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/pane.resize", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/pane.kill", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/pane.rename", System.StringComparison.Ordinal);
    }

    public static ControlResponse? Check(ControlRequest request, PaneScopeStore scopeStore, WorkspaceManager? workspaces, LayoutService? layout)
    {
        if (request.Params is not JsonElement el)
            return null;
        string? targetPaneId = null;
        if (el.TryGetProperty("paneId", out var pid) && pid.ValueKind == JsonValueKind.String)
            targetPaneId = pid.GetString();
        if (targetPaneId is null)
            return null;
        var callerPaneId = request.CallerPaneId;
        if (string.IsNullOrEmpty(callerPaneId))
            return null;
        var callerScope = scopeStore.GetScope(callerPaneId!);
        var resolver = new ScopeResolver(workspaces);
        var (callerWs, callerRoom) = resolver.ResolvePaneLocation(callerPaneId);
        var (targetWs, targetRoom) = resolver.ResolvePaneLocation(targetPaneId);
        var gate = new ScopeGate();
        var result = gate.CheckAccess(callerPaneId, callerWs, callerRoom, targetPaneId, targetWs, targetRoom, callerScope);
        if (!result.Allowed)
            return new ControlResponse(request.Id, false, null, new ControlError(result.ErrorCode ?? "access_denied", result.Message ?? "access denied"));
        return null;
    }
}
