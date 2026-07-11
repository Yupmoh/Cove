namespace Cove.Engine.Protocol;

public enum McpScope { SameTab, SameBay, All }

public sealed record ScopeAccessResult(bool Allowed, string? ErrorCode, string? Message);

public sealed class ScopeGate
{
    public ScopeAccessResult CheckAccess(
        string? callerNookId, string? callerBayId, string? callerShoreId,
        string? targetNookId, string? targetBayId, string? targetShoreId,
        McpScope scope)
    {
        if (scope == McpScope.All)
            return new ScopeAccessResult(true, null, null);

        if (callerNookId is null)
            return new ScopeAccessResult(true, null, null);

        if (targetNookId is null && targetBayId is null)
            return new ScopeAccessResult(false, "not_found", "target nook not found");

        if (scope == McpScope.SameTab)
        {
            if (targetNookId == callerNookId)
                return new ScopeAccessResult(true, null, null);
            return new ScopeAccessResult(false, "access_denied", $"nook {callerNookId} scoped same-tab cannot access {targetNookId}");
        }

        if (scope == McpScope.SameBay)
        {
            if (callerBayId is not null && callerBayId == targetBayId)
                return new ScopeAccessResult(true, null, null);
            return new ScopeAccessResult(false, "access_denied", $"nook {callerNookId} scoped same-bay cannot access bay {targetBayId}");
        }

        return new ScopeAccessResult(true, null, null);
    }
}
