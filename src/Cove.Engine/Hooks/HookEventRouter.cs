using System.Collections.Concurrent;
using Cove.Adapters;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Hooks;

public sealed record PaneAgentState(
    string PaneId,
    string Adapter,
    string Status,
    int ActiveSubagents,
    System.DateTimeOffset LastEventAt)
{
    public PaneAgentState WithStatus(string status) => this with { Status = status, LastEventAt = System.DateTimeOffset.UtcNow };
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

    private readonly ConcurrentDictionary<string, PaneAgentState> _paneStates = new();
    private readonly ILogger? _logger;

    public HookEventRouter(ILogger? logger = null)
    {
        _logger = logger;
    }

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

        switch (ev.Event)
        {
            case "session-start":
                _paneStates[ev.PaneId] = new PaneAgentState(ev.PaneId, ev.Adapter, "active", 0, System.DateTimeOffset.UtcNow);
                break;
            case "session-end":
                UpdateState(ev.PaneId, s => s.WithStatus("idle"));
                break;
            case "stop":
                UpdateState(ev.PaneId, s => s.WithStatus("needs-input"));
                break;
            case "stop-failure":
                UpdateState(ev.PaneId, s => s.WithStatus("error"));
                break;
            case "user-prompt-submit":
                UpdateState(ev.PaneId, s => s.WithStatus("active"));
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
                break;
        }
    }

    private void UpdateState(string paneId, System.Func<PaneAgentState, PaneAgentState> update)
    {
        _paneStates.AddOrUpdate(
            paneId,
            _ => new PaneAgentState(paneId, "unknown", "active", 0, System.DateTimeOffset.UtcNow),
            (_, existing) => update(existing));
    }

    public PaneAgentState? GetPaneState(string paneId) => _paneStates.TryGetValue(paneId, out var s) ? s : null;

    public IReadOnlyDictionary<string, PaneAgentState> GetAllPaneStates() => _paneStates;
}
