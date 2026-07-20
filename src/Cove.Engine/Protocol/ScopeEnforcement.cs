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

public enum ScopePolicy
{
    Unspecified,
    ControlOnly,
    NookAllowed,
    SelfOnly,
    TargetScoped,
    PlacementScoped,
    ListScoped,
    LayoutRead,
    LayoutMutation
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
        PolicyFor(uri) != ScopePolicy.Unspecified
        || IsNookTargetingVerb(uri)
        || IsExplicitNookAllowedVerb(uri)
        || IsControlOnlyVerb(uri);

    public static bool IsAgentControlVerb(string uri) =>
        uri.StartsWith(
            "cove://commands/agent.",
            StringComparison.Ordinal)
        || uri.StartsWith(
            "cove://commands/launch-profile.",
            StringComparison.Ordinal)
        || uri.StartsWith(
            "cove://commands/layout.",
            StringComparison.Ordinal)
        || uri.StartsWith(
            "cove://commands/nook.",
            StringComparison.Ordinal)
        || uri.StartsWith(
            "cove://commands/session.",
            StringComparison.Ordinal)
        || uri is "cove://commands/hook.emit"
            or "cove://commands/nook-types.list"
            or "cove://commands/vault.reindex"
            or "cove://commands/vault.resume"
            or "cove://commands/vault.search"
            or "cove://commands/vault.set-setting"
            or "cove://commands/workspace.context";

    public static ScopePolicy PolicyFor(string uri) => uri switch
    {
        "cove://commands/agent.close" =>
            ScopePolicy.TargetScoped,
        "cove://commands/agent.definition.delete" =>
            ScopePolicy.ControlOnly,
        "cove://commands/agent.definition.list" =>
            ScopePolicy.NookAllowed,
        "cove://commands/agent.definition.show" =>
            ScopePolicy.NookAllowed,
        "cove://commands/agent.launch" =>
            ScopePolicy.PlacementScoped,
        "cove://commands/agent.list" =>
            ScopePolicy.ListScoped,
        "cove://commands/agent.message" =>
            ScopePolicy.TargetScoped,
        "cove://commands/agent.replay" =>
            ScopePolicy.TargetScoped,
        "cove://commands/agent.spawned-nooks" =>
            ScopePolicy.TargetScoped,
        "cove://commands/agent.stop" =>
            ScopePolicy.TargetScoped,
        "cove://commands/hook.emit" =>
            ScopePolicy.SelfOnly,
        "cove://commands/launch-profile.create" =>
            ScopePolicy.ControlOnly,
        "cove://commands/launch-profile.delete" =>
            ScopePolicy.ControlOnly,
        "cove://commands/launch-profile.get" =>
            ScopePolicy.NookAllowed,
        "cove://commands/launch-profile.list" =>
            ScopePolicy.NookAllowed,
        "cove://commands/launch-profile.set-default" =>
            ScopePolicy.ControlOnly,
        "cove://commands/launch-profile.update" =>
            ScopePolicy.ControlOnly,
        "cove://commands/layout.get" =>
            ScopePolicy.LayoutRead,
        "cove://commands/layout.mutate" =>
            ScopePolicy.LayoutMutation,
        "cove://commands/layout.snapshot" =>
            ScopePolicy.LayoutRead,
        "cove://commands/nook-types.list" =>
            ScopePolicy.NookAllowed,
        "cove://commands/nook.checkpoint" =>
            ScopePolicy.TargetScoped,
        "cove://commands/nook.kill" =>
            ScopePolicy.TargetScoped,
        "cove://commands/nook.list" =>
            ScopePolicy.ControlOnly,
        "cove://commands/nook.open" =>
            ScopePolicy.PlacementScoped,
        "cove://commands/nook.read" =>
            ScopePolicy.TargetScoped,
        "cove://commands/nook.rename" =>
            ScopePolicy.TargetScoped,
        "cove://commands/nook.resize" =>
            ScopePolicy.TargetScoped,
        "cove://commands/nook.restart" =>
            ScopePolicy.TargetScoped,
        "cove://commands/nook.scope.get" =>
            ScopePolicy.TargetScoped,
        "cove://commands/nook.scope.set" =>
            ScopePolicy.ControlOnly,
        "cove://commands/nook.search" =>
            ScopePolicy.TargetScoped,
        "cove://commands/nook.spawn" =>
            ScopePolicy.ControlOnly,
        ControlProtocolRoutes.NookSubscribe =>
            ScopePolicy.TargetScoped,
        "cove://commands/nook.write" =>
            ScopePolicy.TargetScoped,
        "cove://commands/session.background" =>
            ScopePolicy.TargetScoped,
        "cove://commands/session.dismiss" =>
            ScopePolicy.TargetScoped,
        "cove://commands/session.foreground" =>
            ScopePolicy.TargetScoped,
        "cove://commands/session.list" =>
            ScopePolicy.ControlOnly,
        "cove://commands/session.recent" =>
            ScopePolicy.NookAllowed,
        "cove://commands/session.state" =>
            ScopePolicy.TargetScoped,
        "cove://commands/session.stop" =>
            ScopePolicy.TargetScoped,
        "cove://commands/vault.reindex" =>
            ScopePolicy.ControlOnly,
        "cove://commands/vault.resume" =>
            ScopePolicy.NookAllowed,
        "cove://commands/vault.search" =>
            ScopePolicy.NookAllowed,
        "cove://commands/vault.set-setting" =>
            ScopePolicy.ControlOnly,
        "cove://commands/workspace.context" =>
            ScopePolicy.TargetScoped,
        _ => ScopePolicy.Unspecified
    };

    public static bool IsNookTargetingVerb(string uri)
    {
        if (PolicyFor(uri) == ScopePolicy.TargetScoped)
            return true;
        return uri switch
        {
            "cove://commands/send_to_agent" => true,
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
        request = request with
        {
            CallerNookId = principal.NookId,
        };
        var policy = PolicyFor(request.Uri);
        if (policy == ScopePolicy.ControlOnly
            || IsControlOnlyVerb(request.Uri))
        {
            return Denied(
                request.Id,
                "command requires the daemon control capability");
        }
        if (policy == ScopePolicy.TargetScoped)
            return Check(request, scopeStore, bays, layout, agentRouter);
        if (policy == ScopePolicy.SelfOnly)
            return CheckSelf(request);
        if (policy == ScopePolicy.PlacementScoped)
        {
            return CheckPlacement(
                request,
                scopeStore,
                layout);
        }
        if (policy == ScopePolicy.ListScoped)
            return CheckAgentList(request, scopeStore);
        if (policy == ScopePolicy.LayoutRead)
            return CheckLayoutRead(request, scopeStore, layout);
        if (policy == ScopePolicy.LayoutMutation)
        {
            return CheckLayoutMutation(
                request,
                scopeStore,
                layout);
        }
        if (policy == ScopePolicy.NookAllowed)
            return null;
        if (IsNookTargetingVerb(request.Uri))
            return Check(request, scopeStore, bays, layout, agentRouter);
        if (IsExplicitNookAllowedVerb(request.Uri))
            return null;
        if (IsAgentControlVerb(request.Uri))
        {
            return Denied(
                request.Id,
                "command is not represented by the agent control policy");
        }
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
        uri is "cove://sys/daemon.stop"
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
        if (PolicyFor(uri) == ScopePolicy.NookAllowed)
            return true;
        return uri switch
        {
            "cove://sys/ping" => true,
            "cove://sys/daemon.status" => true,
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

    private static ControlResponse? CheckSelf(
        ControlRequest request)
    {
        var targetNookId = request.Params
            is JsonElement
            {
                ValueKind: JsonValueKind.Object
            } element
            && TryGetString(
                element,
                "nookId",
                out var explicitTarget)
            ? explicitTarget
            : request.CallerNookId;
        return string.Equals(
            request.CallerNookId,
            targetNookId,
            StringComparison.Ordinal)
            ? null
            : Denied(
                request.Id,
                "command is restricted to the caller nook");
    }

    private static ControlResponse? CheckPlacement(
        ControlRequest request,
        NookScopeStore scopeStore,
        LayoutService? layout)
    {
        if (layout is null)
        {
            return Denied(
                request.Id,
                "layout service is unavailable");
        }
        if (request.Params is not JsonElement element)
            return Invalid(request.Id, "placement params are required");
        string? requestedRelativeNookId;
        string? requestedBayId;
        string placement;
        if (request.Uri == "cove://commands/agent.launch")
        {
            var parameters = element.Deserialize(
                CoveJsonContext.Default.AgentLaunchParams);
            if (parameters is null)
                return Invalid(request.Id, "agent launch params are required");
            requestedRelativeNookId = parameters.RelativeToNookId;
            requestedBayId = parameters.BayId;
            placement = parameters.Placement;
        }
        else
        {
            var parameters = element.Deserialize(
                CoveJsonContext.Default.NookOpenParams);
            if (parameters is null)
                return Invalid(request.Id, "nook open params are required");
            requestedRelativeNookId = parameters.RelativeToNookId;
            requestedBayId = parameters.BayId;
            placement = parameters.Placement;
        }
        if (placement is not (
            "left" or "right" or "above" or "below"
            or "new-shore"))
        {
            return Invalid(
                request.Id,
                "nook placement is invalid");
        }
        var callerNookId = request.CallerNookId!;
        var relativeNookId =
            requestedRelativeNookId ?? callerNookId;
        var relativeLocation =
            layout.ResolveNookLocation(relativeNookId);
        if (relativeLocation.BayId is null
            || relativeLocation.ShoreId is null)
        {
            return Invalid(
                request.Id,
                "relative nook is not placed");
        }
        if (requestedBayId is not null
            && !string.Equals(
                requestedBayId,
                relativeLocation.BayId,
                StringComparison.Ordinal))
        {
            return Invalid(
                request.Id,
                "bayId must match the relative nook bay");
        }
        return CheckBoundary(
            request.Id,
            callerNookId,
            relativeLocation.BayId,
            placement == "new-shore"
                ? null
                : relativeLocation.ShoreId,
            scopeStore,
            layout,
            "nook placement exceeds caller scope");
    }

    private static ControlResponse? CheckAgentList(
        ControlRequest request,
        NookScopeStore scopeStore)
    {
        var parameters = request.Params is JsonElement element
            ? element.Deserialize(
                CoveJsonContext.Default.AgentListParams)
            : null;
        var requested = parameters?.Scope ?? "same-tab";
        var callerScope = scopeStore.GetScope(
            request.CallerNookId!);
        var allowed = requested switch
        {
            "same-tab" => true,
            "same-bay" =>
                callerScope is McpScope.SameBay or McpScope.All,
            "all" => callerScope == McpScope.All,
            _ => false,
        };
        return allowed
            ? null
            : Denied(
                request.Id,
                "agent list scope exceeds caller scope");
    }

    private static ControlResponse? CheckLayoutRead(
        ControlRequest request,
        NookScopeStore scopeStore,
        LayoutService? layout)
    {
        if (layout is null)
        {
            return Denied(
                request.Id,
                "layout service is unavailable");
        }
        var callerNookId = request.CallerNookId!;
        var callerLocation =
            layout.ResolveNookLocation(callerNookId);
        var targetBayId = request.Params is JsonElement element
            ? element.Deserialize(
                CoveJsonContext.Default.LayoutGetParams)?.BayId
            : null;
        targetBayId ??= callerLocation.BayId;
        if (targetBayId is null)
        {
            return Invalid(
                request.Id,
                "caller nook is not placed");
        }
        var scope = scopeStore.GetScope(callerNookId);
        var allowed = scope switch
        {
            McpScope.SameTab => false,
            McpScope.SameBay =>
                callerLocation.BayId == targetBayId,
            McpScope.All => true,
            _ => false,
        };
        return allowed
            ? null
            : Denied(
                request.Id,
                "layout inspection exceeds caller scope");
    }

    private static ControlResponse? CheckLayoutMutation(
        ControlRequest request,
        NookScopeStore scopeStore,
        LayoutService? layout)
    {
        if (layout is null)
        {
            return Denied(
                request.Id,
                "layout service is unavailable");
        }
        if (request.Params is not JsonElement element
            || element.Deserialize(
                CoveJsonContext.Default.LayoutMutateParams)
                is not { } parameters)
        {
            return Invalid(
                request.Id,
                "layout mutate params are required");
        }
        var callerNookId = request.CallerNookId!;
        var scope = scopeStore.GetScope(callerNookId);
        if (parameters.Op is "createShore" or "reorder")
        {
            return scope == McpScope.All
                ? null
                : Denied(
                    request.Id,
                    "workspace-wide layout mutation requires all scope");
        }
        var checkedBoundary = false;
        if (!string.IsNullOrEmpty(parameters.ShoreId))
        {
            var owner = ResolveShoreLocation(
                layout,
                parameters.ShoreId);
            if (owner.BayId is null)
            {
                return Invalid(
                    request.Id,
                    "target shore is not placed");
            }
            var denied = CheckBoundary(
                request.Id,
                callerNookId,
                owner.BayId,
                owner.ShoreId,
                scopeStore,
                layout,
                "layout mutation exceeds caller scope");
            if (denied is not null)
                return denied;
            checkedBoundary = true;
        }
        foreach (var nookId in new[]
        {
            parameters.NookId,
            parameters.TargetNookId,
        })
        {
            if (string.IsNullOrEmpty(nookId))
                continue;
            var location = layout.ResolveNookLocation(nookId);
            if (location.BayId is null)
            {
                return Invalid(
                    request.Id,
                    $"nook {nookId} is not placed");
            }
            var denied = CheckBoundary(
                request.Id,
                callerNookId,
                location.BayId,
                location.ShoreId,
                scopeStore,
                layout,
                "layout mutation exceeds caller scope");
            if (denied is not null)
                return denied;
            checkedBoundary = true;
        }
        return checkedBoundary
            ? null
            : Invalid(
                request.Id,
                "layout mutation target is required");
    }

    private static (string? BayId, string? ShoreId)
        ResolveShoreLocation(
            LayoutService layout,
            string shoreId)
    {
        foreach (var bayId in layout.BayIds)
        {
            if (layout.ShoresFor(bayId).Any(
                shore => shore.Id == shoreId))
            {
                return (bayId, shoreId);
            }
        }
        return (null, null);
    }

    private static ControlResponse? CheckBoundary(
        string requestId,
        string callerNookId,
        string targetBayId,
        string? targetShoreId,
        NookScopeStore scopeStore,
        LayoutService layout,
        string deniedMessage)
    {
        var callerLocation =
            layout.ResolveNookLocation(callerNookId);
        var allowed = scopeStore.GetScope(callerNookId) switch
        {
            McpScope.SameTab =>
                targetShoreId is not null
                && callerLocation.ShoreId == targetShoreId,
            McpScope.SameBay =>
                callerLocation.BayId == targetBayId,
            McpScope.All => true,
            _ => false,
        };
        return allowed
            ? null
            : Denied(requestId, deniedMessage);
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

    private static ControlResponse Invalid(
        string id,
        string message) =>
        new(
            id,
            false,
            null,
            new ControlError("invalid_params", message));
}
