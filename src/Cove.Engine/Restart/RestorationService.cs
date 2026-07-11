using System.Text.Json.Serialization;
using Cove.Persistence;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Restart;

public sealed record RestoreProgressEvent(string BayId, string Step, RestorePhase Phase, string? Detail = null);

public enum RestorePhase { Started, BayLoaded, NooksMaterialized, Completed, Failed }

public sealed class RestorationService
{
    private readonly string _stateDir;
    private readonly ILogger _logger;
    private readonly Action<RestoreProgressEvent>? _emitProgress;
    public ILogger Logger => _logger;
    public RestorationService(string stateDir, ILogger logger, Action<RestoreProgressEvent>? emitProgress = null)
    {
        _stateDir = stateDir;
        _logger = logger;
        _emitProgress = emitProgress;
    }

    public string StatePath => Path.Combine(_stateDir, "state.json");

    public CoveState LoadState()
    {
        var state = AtomicJsonStore.Read(StatePath, CoveJsonContext.Default.CoveState, _logger);
        return state ?? new CoveState();
    }

    public bool WasCleanShutdown()
    {
        var state = LoadState();
        return state.CleanShutdown;
    }

    public void MarkLaunching()
    {
        var state = LoadState();
        var updated = state with { CleanShutdown = false, ShutdownAtUtc = null };
        AtomicJsonStore.Write(StatePath, updated, CoveJsonContext.Default.CoveState);
    }

    public void MarkCleanShutdown()
    {
        var state = LoadState();
        var updated = state with { CleanShutdown = true, ShutdownAtUtc = DateTimeOffset.UtcNow };
        AtomicJsonStore.Write(StatePath, updated, CoveJsonContext.Default.CoveState);
    }

    public void SaveState(CoveState state)
        => AtomicJsonStore.Write(StatePath, state, CoveJsonContext.Default.CoveState);

    public void EmitProgress(string bayId, string step, RestorePhase phase, string? detail = null)
        => _emitProgress?.Invoke(new RestoreProgressEvent(bayId, step, phase, detail));
}

public sealed record RestoreStateResult(bool WasClean, IReadOnlyList<string> OpenBays, string? FocusedBay);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RestoreProgressEvent))]
[JsonSerializable(typeof(RestoreStateResult))]
public sealed partial class RestorationJsonContext : JsonSerializerContext { }
