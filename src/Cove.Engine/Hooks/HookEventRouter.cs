using System.Collections.Concurrent;
using System.Text.Json;
using Cove.Adapters;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Hooks;

public sealed record PaneAgentState(
    string PaneId,
    string Adapter,
    string Status,
    int ActiveSubagents,
    System.DateTimeOffset LastEventAt,
    string? StopReason = null)
{
    public PaneAgentState WithStatus(string status) => this with { Status = status, LastEventAt = System.DateTimeOffset.UtcNow };
    public PaneAgentState WithStop(string? reason) => this with { Status = "error", StopReason = reason, LastEventAt = System.DateTimeOffset.UtcNow };
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

    private readonly ConcurrentDictionary<string, PaneAgentState> _paneStates = new();
    private readonly ILogger? _logger;

    public HookEventRouter(ILogger? logger = null)
    {
        _logger = logger;
    }

    public event Action<string, bool>? NeedsInputTransition;

    public void Route(HookEvent ev)
    {
        if (ev.PaneId is null)
        {
            _logger?.HookEventNoPaneId(ev.Adapter, ev.Event);
            return;
        }

        if (!DeclaredEvents.Contains(ev.Event))
        {
            _logger?.HookEventUnknown(ev.Adapter, ev.Event);
            return;
        }

        if (ev.Event != "session-start" && !_paneStates.ContainsKey(ev.PaneId))
        {
            _logger?.HookEventUntrackedPane(ev.Adapter, ev.Event, ev.PaneId);
            return;
        }

        switch (ev.Event)
        {
            case "session-start":
                _paneStates[ev.PaneId] = new PaneAgentState(ev.PaneId, ev.Adapter, "active", 0, System.DateTimeOffset.UtcNow);
                break;
            case "session-end":
                UpdateState(ev.PaneId, s => s with { Status = "idle", StopReason = null, LastEventAt = System.DateTimeOffset.UtcNow });
                NeedsInputTransition?.Invoke(ev.PaneId, false);
                break;
            case "stop":
                UpdateState(ev.PaneId, s => s.WithStatus("needs-input"));
                NeedsInputTransition?.Invoke(ev.PaneId, true);
                break;
            case "stop-failure":
                var reason = ExtractStopReason(ev.Payload);
                UpdateState(ev.PaneId, s => s.WithStop(reason));
                break;
            case "user-prompt-submit":
                UpdateState(ev.PaneId, s => s.WithStatus("active"));
                NeedsInputTransition?.Invoke(ev.PaneId, false);
                break;
            case "pre-tool-use":
                UpdateState(ev.PaneId, s => s.WithStatus("tool-running"));
                break;
            case "post-tool-use":
                UpdateState(ev.PaneId, s => s.WithStatus("active"));
                break;
            case "subagent-start":
                UpdateState(ev.PaneId, s => s with { ActiveSubagents = s.ActiveSubagents + 1, LastEventAt = System.DateTimeOffset.UtcNow });
                break;
            case "subagent-stop":
                UpdateState(ev.PaneId, s => s with { ActiveSubagents = System.Math.Max(0, s.ActiveSubagents - 1), LastEventAt = System.DateTimeOffset.UtcNow });
                break;
            case "notification":
            case "permission-request":
                UpdateState(ev.PaneId, s => s with { LastEventAt = System.DateTimeOffset.UtcNow });
                NeedsInputTransition?.Invoke(ev.PaneId, true);
                break;
        }
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

    private void UpdateState(string paneId, System.Func<PaneAgentState, PaneAgentState> update)
    {
        while (_paneStates.TryGetValue(paneId, out var existing))
        {
            if (_paneStates.TryUpdate(paneId, update(existing), existing))
                return;
        }
    }

    public PaneAgentState? GetPaneState(string paneId) => _paneStates.TryGetValue(paneId, out var s) ? s : null;

    public IReadOnlyDictionary<string, PaneAgentState> GetAllPaneStates() => _paneStates;
}
