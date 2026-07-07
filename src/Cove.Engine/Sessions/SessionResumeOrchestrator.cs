using Microsoft.Extensions.Logging;

namespace Cove.Engine.Sessions;

public enum SessionLifecycle
{
    Active,
    Dismissed,
    Background,
    Waking,
    Cancelled,
}

public sealed record SessionState
{
    public required string PaneId { get; init; }
    public required string Adapter { get; init; }
    public string? SessionId { get; init; }
    public required SessionLifecycle Lifecycle { get; init; }
    public required bool Resumable { get; init; }
}

public sealed class SessionResumeOrchestrator
{
    private readonly Dictionary<string, SessionState> _states = new();
    private readonly ILogger? _logger;

    public SessionResumeOrchestrator(ILogger? logger = null)
    {
        _logger = logger;
    }

    public void Register(string paneId, string adapter, string? sessionId)
    {
        _states[paneId] = new SessionState { PaneId = paneId, Adapter = adapter, SessionId = sessionId, Lifecycle = SessionLifecycle.Active, Resumable = true };
    }

    public void Unregister(string paneId)
    {
        _states.Remove(paneId);
    }

    public SessionState? GetState(string paneId)
    {
        return _states.TryGetValue(paneId, out var state) ? state : null;
    }

    public void Dismiss(string paneId)
    {
        if (!_states.TryGetValue(paneId, out var state))
        {
            _logger?.SessionUnknownPane("dismiss", paneId);
            return;
        }
        _states[paneId] = state with { Lifecycle = SessionLifecycle.Dismissed, Resumable = true };
    }

    public void Background(string paneId)
    {
        if (!_states.TryGetValue(paneId, out var state))
        {
            _logger?.SessionUnknownPane("background", paneId);
            return;
        }
        _states[paneId] = state with { Lifecycle = SessionLifecycle.Background, Resumable = true };
    }

    public void Foreground(string paneId)
    {
        if (!_states.TryGetValue(paneId, out var state))
        {
            _logger?.SessionUnknownPane("foreground", paneId);
            return;
        }
        _states[paneId] = state with { Lifecycle = SessionLifecycle.Active };
    }

    public void Stop(string paneId)
    {
        if (!_states.TryGetValue(paneId, out var state))
        {
            _logger?.SessionUnknownPane("stop", paneId);
            return;
        }
        _states[paneId] = state with { Lifecycle = SessionLifecycle.Cancelled, Resumable = false };
    }

    public bool CanWake(string paneId)
    {
        if (!_states.TryGetValue(paneId, out var state))
            return false;
        return state.Lifecycle == SessionLifecycle.Dismissed && state.Resumable;
    }

    public void MarkWaking(string paneId)
    {
        if (!_states.TryGetValue(paneId, out var state))
            return;
        _states[paneId] = state with { Lifecycle = SessionLifecycle.Waking };
    }

    public void MarkActive(string paneId)
    {
        if (!_states.TryGetValue(paneId, out var state))
            return;
        _states[paneId] = state with { Lifecycle = SessionLifecycle.Active };
    }

    public IEnumerable<SessionState> ListDismissed()
    {
        return _states.Values.Where(s => s.Lifecycle == SessionLifecycle.Dismissed && s.Resumable);
    }

    public IEnumerable<SessionState> ListBackground()
    {
        return _states.Values.Where(s => s.Lifecycle == SessionLifecycle.Background);
    }
}
