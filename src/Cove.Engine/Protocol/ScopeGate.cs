namespace Cove.Engine.Protocol;

public enum McpScope { SameTab, SameWorkspace, All }

public sealed record ScopeAccessResult(bool Allowed, string? ErrorCode, string? Message);

public sealed class ScopeGate
{
    public ScopeAccessResult CheckAccess(
        string? callerPaneId, string? callerWorkspaceId, string? callerRoomId,
        string? targetPaneId, string? targetWorkspaceId, string? targetRoomId,
        McpScope scope)
    {
        if (scope == McpScope.All)
            return new ScopeAccessResult(true, null, null);

        if (callerPaneId is null)
            return new ScopeAccessResult(true, null, null);

        if (targetPaneId is null && targetWorkspaceId is null)
            return new ScopeAccessResult(false, "not_found", "target pane not found");

        if (scope == McpScope.SameTab)
        {
            if (targetPaneId == callerPaneId)
                return new ScopeAccessResult(true, null, null);
            return new ScopeAccessResult(false, "access_denied", $"pane {callerPaneId} scoped same-tab cannot access {targetPaneId}");
        }

        if (scope == McpScope.SameWorkspace)
        {
            if (callerWorkspaceId is not null && callerWorkspaceId == targetWorkspaceId)
                return new ScopeAccessResult(true, null, null);
            return new ScopeAccessResult(false, "access_denied", $"pane {callerPaneId} scoped same-workspace cannot access workspace {targetWorkspaceId}");
        }

        return new ScopeAccessResult(true, null, null);
    }
}
