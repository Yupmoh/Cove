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
    public required string NookId { get; init; }
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

    public void Register(string nookId, string adapter, string? sessionId)
    {
        _states[nookId] = new SessionState { NookId = nookId, Adapter = adapter, SessionId = sessionId, Lifecycle = SessionLifecycle.Active, Resumable = true };
    }

    public void SetSessionId(string nookId, string adapter, string sessionId)
    {
        if (_states.TryGetValue(nookId, out var state))
            _states[nookId] = state with { SessionId = sessionId };
        else
            _states[nookId] = new SessionState { NookId = nookId, Adapter = adapter, SessionId = sessionId, Lifecycle = SessionLifecycle.Active, Resumable = true };
    }

    public void Unregister(string nookId)
    {
        _states.Remove(nookId);
    }

    public SessionState? GetState(string nookId)
    {
        return _states.TryGetValue(nookId, out var state) ? state : null;
    }

    public void Dismiss(string nookId)
    {
        if (!_states.TryGetValue(nookId, out var state))
        {
            _logger?.SessionUnknownNook("dismiss", nookId);
            return;
        }
        _states[nookId] = state with { Lifecycle = SessionLifecycle.Dismissed, Resumable = true };
    }

    public void Background(string nookId)
    {
        if (!_states.TryGetValue(nookId, out var state))
        {
            _logger?.SessionUnknownNook("background", nookId);
            return;
        }
        _states[nookId] = state with { Lifecycle = SessionLifecycle.Background, Resumable = true };
    }

    public void Foreground(string nookId)
    {
        if (!_states.TryGetValue(nookId, out var state))
        {
            _logger?.SessionUnknownNook("foreground", nookId);
            return;
        }
        _states[nookId] = state with { Lifecycle = SessionLifecycle.Active };
    }

    public void Stop(string nookId)
    {
        if (!_states.TryGetValue(nookId, out var state))
        {
            _logger?.SessionUnknownNook("stop", nookId);
            return;
        }
        _states[nookId] = state with { Lifecycle = SessionLifecycle.Cancelled, Resumable = false };
    }

    public bool CanWake(string nookId)
    {
        if (!_states.TryGetValue(nookId, out var state))
            return false;
        return state.Lifecycle == SessionLifecycle.Dismissed && state.Resumable;
    }

    public void MarkWaking(string nookId)
    {
        if (!_states.TryGetValue(nookId, out var state))
            return;
        _states[nookId] = state with { Lifecycle = SessionLifecycle.Waking };
    }

    public void MarkActive(string nookId)
    {
        if (!_states.TryGetValue(nookId, out var state))
            return;
        _states[nookId] = state with { Lifecycle = SessionLifecycle.Active };
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
