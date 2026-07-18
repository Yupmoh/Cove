using System.Text.Json;
using Cove.Engine.Agents;
using Cove.Engine.Bays;
using Cove.Engine.Layout;
using Cove.Protocol;

namespace Cove.Engine.Protocol;

internal static class ScopeEnforcement
{
    public static bool IsNookTargetingVerb(string uri)
    {
        return uri switch
        {
            "cove://commands/nook.write" => true,
            "cove://commands/nook.resize" => true,
            "cove://commands/nook.kill" => true,
            "cove://commands/nook.rename" => true,
            "cove://commands/send_to_agent" => true,
            "cove://commands/agent.message" => true,
            "cove://commands/canvas.action" => true,
            "cove://commands/browser.click" => true,
            "cove://commands/browser.fill" => true,
            "cove://commands/browser.type" => true,
            "cove://commands/browser.press" => true,
            "cove://commands/browser.eval" => true,
            "cove://commands/browser.select" => true,
            "cove://commands/browser.scroll" => true,
            "cove://commands/browser.setUserAgent" => true,
            "cove://commands/browser.clear" => true,
            _ => false
        };
    }

    private static string? ResolveTargetNookId(ControlRequest request, AgentMessageRouter? agentRouter)
    {
        if (request.Params is not JsonElement { ValueKind: JsonValueKind.Object } el)
            return null;

        if (request.Uri == "cove://commands/canvas.action")
        {
            if (!TryGetString(el, "action", out var action) || action != "send_to_agent")
                return null;
            return TryGetString(el, "targetNook", out var canvasTarget) ? canvasTarget : null;
        }

        if (request.Uri == "cove://commands/send_to_agent")
            return TryGetString(el, "targetNook", out var sendTarget) ? sendTarget : null;

        if (request.Uri == "cove://commands/agent.message")
        {
            if (TryGetString(el, "nookId", out var agentNookId))
                return agentNookId;
            if (!TryGetString(el, "target", out var agentTarget))
                return null;
            return agentRouter?.ResolveTarget(agentTarget!)?.NookId ?? agentTarget;
        }

        return TryGetString(el, "nookId", out var targetNookId) ? targetNookId : null;
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return !string.IsNullOrEmpty(value);
        }

        value = null;
        return false;
    }

    public static ControlResponse? Check(
        ControlRequest request,
        NookScopeStore scopeStore,
        BayManager? bays,
        LayoutService? layout,
        AgentMessageRouter? agentRouter)
    {
        var targetNookId = ResolveTargetNookId(request, agentRouter);
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
