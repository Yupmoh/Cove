using Microsoft.Extensions.Logging;

namespace Cove.Engine.Lifecycle;

public enum LifecycleState
{
    Active,
    Stopped,
    Closed,
    Errored,
}

public sealed record AgentLifecycleState(
    string PaneId,
    string Adapter,
    LifecycleState State,
    bool PanePreserved,
    string? SurfacedCommand,
    int? ExitCode,
    int? Signal);

public sealed record ReplayInfo(string Command, int? ExitCode, int? Signal);

public sealed class AgentLifecycleController
{
    private readonly Dictionary<string, AgentLifecycleState> _states = new();
    private readonly Dictionary<string, List<string>> _spawnedPanes = new();
    private readonly ILogger? _logger;

    public AgentLifecycleController(ILogger? logger = null)
    {
        _logger = logger;
    }

    public void Register(string paneId, string adapter)
    {
        _states[paneId] = new AgentLifecycleState(paneId, adapter, LifecycleState.Active, PanePreserved: true, SurfacedCommand: null, ExitCode: null, Signal: null);
    }

    public void Unregister(string paneId)
    {
        _states.Remove(paneId);
        _spawnedPanes.Remove(paneId);
    }

    public AgentLifecycleState? GetState(string paneId)
    {
        return _states.TryGetValue(paneId, out var state) ? state : null;
    }

    public void Stop(string paneId)
    {
        if (!_states.TryGetValue(paneId, out var state))
        {
            _logger?.LifecycleUnknownPane("stop", paneId);
            return;
        }
        _states[paneId] = state with { State = LifecycleState.Stopped, PanePreserved = true };
    }

    public void Close(string paneId)
    {
        if (!_states.TryGetValue(paneId, out var state))
        {
            _logger?.LifecycleUnknownPane("close", paneId);
            return;
        }
        _states[paneId] = state with { State = LifecycleState.Closed, PanePreserved = false };
    }

    public void RecordError(string paneId, string command, int? exitCode, int? signal)
    {
        if (!_states.TryGetValue(paneId, out var state))
        {
            _logger?.LifecycleUnknownPane("error", paneId);
            return;
        }
        _states[paneId] = state with { State = LifecycleState.Errored, SurfacedCommand = command, ExitCode = exitCode, Signal = signal };
    }

    public void ClearError(string paneId)
    {
        if (!_states.TryGetValue(paneId, out var state))
            return;
        _states[paneId] = state with { State = LifecycleState.Active, SurfacedCommand = null, ExitCode = null, Signal = null };
    }

    public ReplayInfo? GetReplayInfo(string paneId)
    {
        if (!_states.TryGetValue(paneId, out var state))
            return null;
        if (state.State != LifecycleState.Errored || state.SurfacedCommand is null)
            return null;
        return new ReplayInfo(state.SurfacedCommand, state.ExitCode, state.Signal);
    }

    public void RecordSpawnedPane(string parentPaneId, string childPaneId)
    {
        if (!_spawnedPanes.ContainsKey(parentPaneId))
            _spawnedPanes[parentPaneId] = new List<string>();
        _spawnedPanes[parentPaneId].Add(childPaneId);
    }

    public IReadOnlyList<string> GetSpawnedPanes(string paneId)
    {
        return _spawnedPanes.TryGetValue(paneId, out var children) ? children : Array.Empty<string>();
    }
}
