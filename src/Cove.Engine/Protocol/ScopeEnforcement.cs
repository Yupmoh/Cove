using System.Text.Json;
using Cove.Engine.Layout;
using Cove.Engine.Bays;
using Cove.Protocol;

namespace Cove.Engine.Protocol;

internal static class ScopeEnforcement
{
    public static bool IsNookTargetingVerb(string uri)
    {
        return uri.StartsWith("cove://commands/nook.write", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/nook.resize", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/nook.kill", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/nook.rename", System.StringComparison.Ordinal);
    }

    public static ControlResponse? Check(ControlRequest request, NookScopeStore scopeStore, BayManager? bays, LayoutService? layout)
    {
        if (request.Params is not JsonElement el)
            return null;
        string? targetNookId = null;
        if (el.TryGetProperty("nookId", out var pid) && pid.ValueKind == JsonValueKind.String)
            targetNookId = pid.GetString();
        if (targetNookId is null)
            return null;
        var callerNookId = request.CallerNookId;
        if (string.IsNullOrEmpty(callerNookId))
            return null;
        var callerScope = scopeStore.GetScope(callerNookId!);
        var resolver = new ScopeResolver(bays);
        var (callerWs, callerShore) = resolver.ResolveNookLocation(callerNookId);
        var (targetWs, targetShore) = resolver.ResolveNookLocation(targetNookId);
        var gate = new ScopeGate();
        var result = gate.CheckAccess(callerNookId, callerWs, callerShore, targetNookId, targetWs, targetShore, callerScope);
        if (!result.Allowed)
            return new ControlResponse(request.Id, false, null, new ControlError(result.ErrorCode ?? "access_denied", result.Message ?? "access denied"));
        return null;
    }
}
