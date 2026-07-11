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
    string NookId,
    string Adapter,
    LifecycleState State,
    bool NookPreserved,
    string? SurfacedCommand,
    int? ExitCode,
    int? Signal);

public sealed record ReplayInfo(string Command, int? ExitCode, int? Signal);

public sealed class AgentLifecycleController
{
    private readonly Dictionary<string, AgentLifecycleState> _states = new();
    private readonly Dictionary<string, List<string>> _spawnedNooks = new();
    private readonly ILogger? _logger;

    public AgentLifecycleController(ILogger? logger = null)
    {
        _logger = logger;
    }

    public void Register(string nookId, string adapter)
    {
        _states[nookId] = new AgentLifecycleState(nookId, adapter, LifecycleState.Active, NookPreserved: true, SurfacedCommand: null, ExitCode: null, Signal: null);
    }

    public void Unregister(string nookId)
    {
        _states.Remove(nookId);
        _spawnedNooks.Remove(nookId);
    }

    public AgentLifecycleState? GetState(string nookId)
    {
        return _states.TryGetValue(nookId, out var state) ? state : null;
    }

    public void Stop(string nookId)
    {
        if (!_states.TryGetValue(nookId, out var state))
        {
            _logger?.LifecycleUnknownNook("stop", nookId);
            return;
        }
        _states[nookId] = state with { State = LifecycleState.Stopped, NookPreserved = true };
    }

    public void Close(string nookId)
    {
        if (!_states.TryGetValue(nookId, out var state))
        {
            _logger?.LifecycleUnknownNook("close", nookId);
            return;
        }
        _states[nookId] = state with { State = LifecycleState.Closed, NookPreserved = false };
    }

    public void RecordError(string nookId, string command, int? exitCode, int? signal)
    {
        if (!_states.TryGetValue(nookId, out var state))
        {
            _logger?.LifecycleUnknownNook("error", nookId);
            return;
        }
        _states[nookId] = state with { State = LifecycleState.Errored, SurfacedCommand = command, ExitCode = exitCode, Signal = signal };
    }

    public void ClearError(string nookId)
    {
        if (!_states.TryGetValue(nookId, out var state))
            return;
        _states[nookId] = state with { State = LifecycleState.Active, SurfacedCommand = null, ExitCode = null, Signal = null };
    }

    public ReplayInfo? GetReplayInfo(string nookId)
    {
        if (!_states.TryGetValue(nookId, out var state))
            return null;
        if (state.State != LifecycleState.Errored || state.SurfacedCommand is null)
            return null;
        return new ReplayInfo(state.SurfacedCommand, state.ExitCode, state.Signal);
    }

    public void RecordSpawnedNook(string parentNookId, string childNookId)
    {
        if (!_spawnedNooks.ContainsKey(parentNookId))
            _spawnedNooks[parentNookId] = new List<string>();
        _spawnedNooks[parentNookId].Add(childNookId);
    }

    public IReadOnlyList<string> GetSpawnedNooks(string nookId)
    {
        return _spawnedNooks.TryGetValue(nookId, out var children) ? children : Array.Empty<string>();
    }
}
