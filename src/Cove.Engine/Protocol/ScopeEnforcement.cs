using System.Text.Json;
using Cove.Engine.Agents;
using Cove.Engine.Bays;
using Cove.Engine.Layout;
using Cove.Protocol;

namespace Cove.Engine.Protocol;

internal enum ConnectionPrincipalKind
{
    Unauthenticated,
    Control,
    Nook
}

internal sealed record ConnectionPrincipal
{
    public static ConnectionPrincipal Unauthenticated { get; } =
        new(ConnectionPrincipalKind.Unauthenticated, "", null);

    private ConnectionPrincipal(
        ConnectionPrincipalKind kind,
        string clientKind,
        string? nookId)
    {
        Kind = kind;
        ClientKind = clientKind;
        NookId = nookId;
    }

    public ConnectionPrincipalKind Kind { get; }
    public string ClientKind { get; }
    public string? NookId { get; }

    public static ConnectionPrincipal Control(string clientKind) =>
        new(ConnectionPrincipalKind.Control, clientKind, null);

    public static ConnectionPrincipal Nook(
        string clientKind,
        string nookId) =>
        new(ConnectionPrincipalKind.Nook, clientKind, nookId);
}

internal static class ScopeEnforcement
{
    public static bool IsRepresentedVerb(string uri) =>
        IsNookTargetingVerb(uri)
        || IsExplicitNookAllowedVerb(uri)
        || IsControlOnlyVerb(uri);

    public static bool IsNookTargetingVerb(string uri)
    {
        return uri switch
        {
            "cove://commands/nook.write" => true,
            "cove://commands/nook.resize" => true,
            "cove://commands/nook.kill" => true,
            "cove://commands/nook.rename" => true,
            "cove://commands/nook.search" => true,
            "cove://commands/nook.read" => true,
            "cove://commands/nook.checkpoint" => true,
            ControlProtocolRoutes.NookSubscribe => true,
            "cove://commands/nook.scope.get" => true,
            "cove://commands/workspace.context" => true,
            "cove://commands/send_to_agent" => true,
            "cove://commands/agent.message" => true,
            "cove://commands/canvas.action" => true,
            "cove://commands/browser.open" => true,
            "cove://commands/browser.navigate" => true,
            "cove://commands/browser.back" => true,
            "cove://commands/browser.forward" => true,
            "cove://commands/browser.reload" => true,
            "cove://commands/browser.close" => true,
            "cove://commands/browser.snapshot" => true,
            "cove://commands/browser.click" => true,
            "cove://commands/browser.fill" => true,
            "cove://commands/browser.eval" => true,
            "cove://commands/browser.screenshot" => true,
            "cove://commands/browser.setUserAgent" => true,
            "cove://commands/browser.clear" => true,
            "cove://commands/browser.type" => true,
            "cove://commands/browser.press" => true,
            "cove://commands/browser.select" => true,
            "cove://commands/browser.scroll" => true,
            "cove://commands/browser.wait" => true,
            "cove://commands/browser.get" => true,
            "cove://commands/browser.is" => true,
            _ => false
        };
    }

    public static ControlResponse? Authorize(
        ConnectionPrincipal principal,
        ControlRequest request,
        NookScopeStore scopeStore,
        BayManager? bays,
        LayoutService? layout,
        AgentMessageRouter? agentRouter)
    {
        if (principal.Kind == ConnectionPrincipalKind.Unauthenticated)
            return Denied(request.Id, "connection is not authenticated");
        if (principal.Kind == ConnectionPrincipalKind.Control)
            return null;
        if (IsControlOnlyVerb(request.Uri))
        {
            return Denied(
                request.Id,
                "command requires the daemon control capability");
        }
        if (IsNookTargetingVerb(request.Uri))
            return Check(request, scopeStore, bays, layout, agentRouter);
        if (IsExplicitNookAllowedVerb(request.Uri))
            return null;
        if (IsScopedDomain(request.Uri))
        {
            return Denied(
                request.Id,
                "command is not represented by the nook authorization policy");
        }
        return null;
    }

    public static ControlResponse? AuthorizeAttributedNook(
        ControlRequest request,
        NookScopeStore scopeStore,
        BayManager? bays,
        LayoutService? layout,
        AgentMessageRouter? agentRouter)
    {
        if (string.IsNullOrEmpty(request.CallerNookId))
            return null;
        return Authorize(
            ConnectionPrincipal.Nook(
                "redrive",
                request.CallerNookId),
            request,
            scopeStore,
            bays,
            layout,
            agentRouter);
    }

    private static bool IsControlOnlyVerb(string uri) =>
        uri is "cove://commands/nook.scope.set"
            or "cove://sys/daemon.stop"
            or "cove://handoff/begin"
            or "cove://commands/browser.automation.result"
            or "cove://commands/dictation.status"
            or "cove://commands/dictation.ensure-model"
            or "cove://commands/dictation.begin"
            or "cove://commands/dictation.started"
            or "cove://commands/dictation.partial"
            or "cove://commands/dictation.stop"
            or "cove://commands/dictation.cancel";

    private static bool IsScopedDomain(string uri) =>
        uri.StartsWith(
            "cove://commands/nook.",
            StringComparison.Ordinal)
        || uri.StartsWith(
            "cove://commands/browser.",
            StringComparison.Ordinal)
        || uri.StartsWith(
            "cove://commands/note.",
            StringComparison.Ordinal)
        || uri.StartsWith(
            "cove://commands/knowledge.",
            StringComparison.Ordinal)
        || uri.StartsWith(
            "cove://commands/canvas.",
            StringComparison.Ordinal)
        || uri.StartsWith(
            "cove://commands/timeline.",
            StringComparison.Ordinal)
        || uri.StartsWith(
            "cove://commands/blackboard.",
            StringComparison.Ordinal)
        || uri.StartsWith(
            "cove://commands/memory.",
            StringComparison.Ordinal)
        || uri.StartsWith(
            "cove://commands/vault.",
            StringComparison.Ordinal)
        || uri.StartsWith(
            "cove://commands/library.",
            StringComparison.Ordinal)
        || uri.StartsWith(
            "cove://commands/review.",
            StringComparison.Ordinal)
        || uri.StartsWith(
            "cove://commands/attribution.",
            StringComparison.Ordinal)
        || uri == "cove://commands/edits.find";

    private static bool IsExplicitNookAllowedVerb(string uri)
    {
        return uri switch
        {
            "cove://sys/ping" => true,
            "cove://sys/daemon.status" => true,
            "cove://commands/hook.emit" => true,
            "cove://commands/nook.list" => true,
            "cove://commands/nook.spawn" => true,
            "cove://commands/agent.launch" => true,
            "cove://commands/browser.create" => true,
            "cove://commands/knowledge.ping" => true,
            "cove://commands/note.create" => true,
            "cove://commands/note.get" => true,
            "cove://commands/note.list" => true,
            "cove://commands/note.update" => true,
            "cove://commands/note.delete" => true,
            "cove://commands/note.search" => true,
            "cove://commands/note.read" => true,
            "cove://commands/note.write" => true,
            "cove://commands/note.history" => true,
            "cove://commands/note.media.save" => true,
            "cove://commands/note.get-state" => true,
            "cove://commands/note.save-state" => true,
            "cove://commands/timeline.append" => true,
            "cove://commands/timeline.list" => true,
            "cove://commands/blackboard.post" => true,
            "cove://commands/blackboard.show" => true,
            "cove://commands/memory.add" => true,
            "cove://commands/memory.search" => true,
            "cove://commands/memory.recall" => true,
            "cove://commands/memory.show" => true,
            "cove://commands/memory.supersede" => true,
            "cove://commands/memory.reindex" => true,
            "cove://commands/memory.consolidate" => true,
            "cove://commands/memory.propose" => true,
            "cove://commands/memory.proposal.transition" => true,
            "cove://commands/edits.find" => true,
            "cove://commands/vault.search" => true,
            "cove://commands/vault.resume" => true,
            "cove://commands/vault.set-setting" => true,
            "cove://commands/vault.reindex" => true,
            "cove://commands/library.list" => true,
            "cove://commands/library.materialize" => true,
            "cove://commands/review.add-comment" => true,
            "cove://commands/review.list-comments" => true,
            "cove://commands/review.resolve" => true,
            "cove://commands/review.reopen" => true,
            "cove://commands/review.close" => true,
            "cove://commands/review.re-anchor" => true,
            "cove://commands/review.audit" => true,
            "cove://commands/review.telemetry" => true,
            "cove://commands/attribution.record" => true,
            "cove://commands/attribution.find-by-line" => true,
            "cove://commands/attribution.find-by-range" => true,
            "cove://commands/attribution.find-by-tool-use" => true,
            "cove://commands/review.dispatch" => true,
            _ => false
        };
    }

    private static string? ResolveTargetNookId(
        ControlRequest request,
        AgentMessageRouter? agentRouter)
    {
        if (request.Uri == "cove://commands/workspace.context"
            && request.Params is not JsonElement
            {
                ValueKind: JsonValueKind.Object
            })
        {
            return request.CallerNookId;
        }
        if (request.Params is not JsonElement
            {
                ValueKind: JsonValueKind.Object
            } element)
        {
            return null;
        }

        if (request.Uri == "cove://commands/canvas.action")
        {
            if (!TryGetString(
                    element,
                    "action",
                    out var action))
            {
                return null;
            }
            if (action == "cove_command")
                return request.CallerNookId;
            if (action != "send_to_agent")
                return null;
            return TryGetString(
                    element,
                    "targetNook",
                    out var canvasTarget)
                ? canvasTarget
                : null;
        }

        if (request.Uri == "cove://commands/send_to_agent")
        {
            return TryGetString(
                    element,
                    "targetNook",
                    out var sendTarget)
                ? sendTarget
                : null;
        }

        if (request.Uri == "cove://commands/agent.message")
        {
            if (TryGetString(
                    element,
                    "nookId",
                    out var agentNookId))
            {
                return agentNookId;
            }
            if (!TryGetString(
                    element,
                    "target",
                    out var agentTarget))
            {
                return null;
            }
            return agentRouter?
                .ResolveTarget(agentTarget!)?
                .NookId
                ?? agentTarget;
        }

        var target = TryGetString(
                element,
                "nookId",
                out var targetNookId)
            ? targetNookId
            : null;
        return request.Uri == "cove://commands/workspace.context"
            ? target ?? request.CallerNookId
            : target;
    }

    private static bool TryGetString(
        JsonElement element,
        string propertyName,
        out string? value)
    {
        if (element.TryGetProperty(
                propertyName,
                out var property)
            && property.ValueKind == JsonValueKind.String)
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
        var callerNookId = request.CallerNookId;
        if (string.IsNullOrEmpty(callerNookId))
            return null;
        var targetNookId = ResolveTargetNookId(
            request,
            agentRouter);
        if (targetNookId is null)
        {
            return new ControlResponse(
                request.Id,
                false,
                null,
                new ControlError(
                    "invalid_params",
                    "scoped command target is required"));
        }
        var callerScope = scopeStore.GetScope(callerNookId);
        var resolver = new ScopeResolver(layout);
        var (callerWorkspace, callerShore) =
            resolver.ResolveNookLocation(callerNookId);
        var (targetWorkspace, targetShore) =
            resolver.ResolveNookLocation(targetNookId);
        var result = new ScopeGate().CheckAccess(
            callerNookId,
            callerWorkspace,
            callerShore,
            targetNookId,
            targetWorkspace,
            targetShore,
            callerScope);
        if (!result.Allowed)
        {
            return new ControlResponse(
                request.Id,
                false,
                null,
                new ControlError(
                    result.ErrorCode ?? "access_denied",
                    result.Message ?? "access denied"));
        }
        return null;
    }

    private static ControlResponse Denied(
        string id,
        string message) =>
        new(
            id,
            false,
            null,
            new ControlError("access_denied", message));
}
