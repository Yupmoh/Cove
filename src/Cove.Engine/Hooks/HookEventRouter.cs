using System.Collections.Concurrent;
using System.Text.Json;
using Cove.Adapters;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Hooks;

public sealed record NookAgentState(
    string NookId,
    string Adapter,
    string Status,
    int ActiveSubagents,
    System.DateTimeOffset LastEventAt,
    string? StopReason = null,
    string? SessionId = null)
{
    public NookAgentState WithStatus(string status) => this with { Status = status, LastEventAt = System.DateTimeOffset.UtcNow };
    public NookAgentState WithStop(string? reason) => this with { Status = "error", StopReason = reason, LastEventAt = System.DateTimeOffset.UtcNow };
}

public sealed class HookEventRouter
{
    public static readonly IReadOnlySet<string> DeclaredEvents = new HashSet<string>
    {
        "session-start", "session-end",
        "pre-tool-use", "post-tool-use",
        "stop", "stop-failure",
        "notification", "user-prompt-submit",
        "permission-request",
        "subagent-start", "subagent-stop",
    };

    private static readonly HashSet<string> SpecialStopReasons = new()
    {
        "rate-limited", "auth-failed", "billing-exceeded",
    };

    private readonly ConcurrentDictionary<string, NookAgentState> _nookStates = new();
    private readonly ILogger? _logger;

    public HookEventRouter(ILogger? logger = null)
    {
        _logger = logger;
    }

    public event Action<string, bool>? NeedsInputTransition;

    public event Action<string, string, string>? SessionStarted;

    public void Seed(string nookId, string adapter, string? sessionId = null)
    {
        if (string.IsNullOrEmpty(nookId))
        {
            _logger?.HookEventNoNookId(adapter, "seed");
            return;
        }
        _nookStates.GetOrAdd(nookId, _ => new NookAgentState(nookId, adapter, "idle", 0, System.DateTimeOffset.UtcNow, SessionId: sessionId));
    }

    public void Route(HookEvent ev)
    {
        if (ev.NookId is null)
        {
            _logger?.HookEventNoNookId(ev.Adapter, ev.Event);
            return;
        }

        _logger?.HookEventReceived(ev.NookId, ev.Adapter, ev.Event);

        if (!DeclaredEvents.Contains(ev.Event))
        {
            _logger?.HookEventUnknown(ev.Adapter, ev.Event);
            return;
        }

        if (ev.Event != "session-start" && !_nookStates.ContainsKey(ev.NookId))
        {
            _logger?.HookEventUntrackedNook(ev.Adapter, ev.Event, ev.NookId);
            return;
        }

        switch (ev.Event)
        {
            case "session-start":
                var sessionId = ExtractSessionId(ev.Payload);
                var priorSessionId = _nookStates.TryGetValue(ev.NookId, out var prior) ? prior.SessionId : null;
                _nookStates[ev.NookId] = new NookAgentState(ev.NookId, ev.Adapter, "idle", 0, System.DateTimeOffset.UtcNow, SessionId: sessionId ?? priorSessionId);
                if (sessionId is not null)
                    SessionStarted?.Invoke(ev.NookId, ev.Adapter, sessionId);
                break;
            case "session-end":
                UpdateState(ev.NookId, s => s with { Status = "idle", StopReason = null, LastEventAt = System.DateTimeOffset.UtcNow });
                NeedsInputTransition?.Invoke(ev.NookId, false);
                break;
            case "stop":
                UpdateState(ev.NookId, s => s.WithStatus("done"));
                NeedsInputTransition?.Invoke(ev.NookId, false);
                break;
            case "stop-failure":
                var reason = ExtractStopReason(ev.Payload);
                UpdateState(ev.NookId, s => s.WithStop(reason));
                break;
            case "user-prompt-submit":
                UpdateState(ev.NookId, s => s.WithStatus("active"));
                NeedsInputTransition?.Invoke(ev.NookId, false);
                break;
            case "pre-tool-use":
                UpdateState(ev.NookId, s => s.WithStatus("tool-running"));
                break;
            case "post-tool-use":
                UpdateState(ev.NookId, s => s.WithStatus("active"));
                break;
            case "subagent-start":
                UpdateState(ev.NookId, s => s with { ActiveSubagents = s.ActiveSubagents + 1, LastEventAt = System.DateTimeOffset.UtcNow });
                break;
            case "subagent-stop":
                UpdateState(ev.NookId, s => s with { ActiveSubagents = System.Math.Max(0, s.ActiveSubagents - 1), LastEventAt = System.DateTimeOffset.UtcNow });
                break;
            case "permission-request":
                UpdateState(ev.NookId, s => s.WithStatus("needs-permission"));
                NeedsInputTransition?.Invoke(ev.NookId, true);
                break;
            case "notification":
                UpdateState(ev.NookId, s => s.WithStatus("needs-input"));
                NeedsInputTransition?.Invoke(ev.NookId, true);
                break;
        }

        if (_logger is { } log && _nookStates.TryGetValue(ev.NookId, out var updated))
            log.HookStateTransition(ev.NookId, ev.Adapter, ev.Event, updated.Status);
    }

    private static string? ExtractSessionId(JsonElement? payload)
    {
        if (payload is not { } el || el.ValueKind != JsonValueKind.Object)
            return null;
        foreach (var key in new[] { "session_id", "sessionId" })
        {
            if (el.TryGetProperty(key, out var idEl) && idEl.ValueKind == JsonValueKind.String)
            {
                var id = idEl.GetString();
                if (!string.IsNullOrEmpty(id))
                    return id;
            }
        }
        return null;
    }

    private static string? ExtractStopReason(JsonElement? payload)
    {
        if (payload is not { } el || el.ValueKind != JsonValueKind.Object)
            return null;
        if (el.TryGetProperty("reason", out var reasonEl) && reasonEl.ValueKind == JsonValueKind.String)
        {
            var reason = reasonEl.GetString();
            if (reason is not null && SpecialStopReasons.Contains(reason))
                return reason;
        }
        if (el.TryGetProperty("error", out var errorEl) && errorEl.ValueKind == JsonValueKind.String)
        {
            var error = errorEl.GetString();
            if (error is not null && SpecialStopReasons.Contains(error))
                return error;
        }
        return null;
    }

    private void UpdateState(string nookId, System.Func<NookAgentState, NookAgentState> update)
    {
        while (_nookStates.TryGetValue(nookId, out var existing))
        {
            if (_nookStates.TryUpdate(nookId, update(existing), existing))
                return;
        }
    }

    public NookAgentState? GetNookState(string nookId) => _nookStates.TryGetValue(nookId, out var s) ? s : null;

    public IReadOnlyDictionary<string, NookAgentState> GetAllNookStates() => _nookStates;
}
