using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Diagnostics;

public sealed record DiagnosticsConfig(bool Enabled, bool WebInspectorOptIn, int MaxSnapshots, TimeSpan SnapshotInterval, bool CaptureTerminalStats = true, bool CaptureMemoryStats = true, int FlushIntervalMs = 2000);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DiagnosticsConfig))]
[JsonSerializable(typeof(DiagnosticsSnapshot))]
[JsonSerializable(typeof(System.Collections.Generic.List<DiagnosticsSnapshot>))]
public sealed partial class DiagnosticsJsonContext : JsonSerializerContext { }

public sealed record DiagnosticsSnapshot(
    System.DateTimeOffset TakenAt,
    long ManagedMemoryBytes,
    long WorkingSetBytes,
    int ThreadCount,
    int GcGen0Collections,
    int GcGen1Collections,
    int GcGen2Collections,
    int ActiveNooks,
    int ActiveBays,
    int ActiveAgents,
    double CpuUsagePercent,
    System.Collections.Generic.Dictionary<string, long> NookScrollbackBytes);

public sealed class DiagnosticsHub
{
    private readonly ILogger _logger;
    private DiagnosticsConfig _config;
    private readonly System.Collections.Generic.List<DiagnosticsSnapshot> _snapshots = new();
    private System.Threading.CancellationTokenSource? _cts;
    private Task? _snapshotTask;

    public DiagnosticsHub(DiagnosticsConfig? config = null, ILogger? logger = null)
    {
        _config = config ?? new DiagnosticsConfig(false, false, 100, TimeSpan.FromMinutes(5));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    public DiagnosticsConfig Config => _config;
    public bool Enabled => _config.Enabled;

    public void Configure(DiagnosticsConfig config)
    {
        _config = config;
        _logger.LogInformation("diagnostics: configured enabled={enabled} inspector={inspector}", config.Enabled, config.WebInspectorOptIn);
        if (config.Enabled && _snapshotTask is null)
            StartSnapshotLoop();
        else if (!config.Enabled && _snapshotTask is not null)
            StopSnapshotLoop();
    }

    public void Enable() => Configure(_config with { Enabled = true });
    public void Disable()
    {
        Configure(_config with { Enabled = false });
        _logger.LogWarning("diagnostics: hub disabled");
    }

    public DiagnosticsSnapshot TakeSnapshot(
        int activeNooks = 0,
        int activeBays = 0,
        int activeAgents = 0,
        System.Collections.Generic.Dictionary<string, long>? nookScrollbackBytes = null)
    {
        var captureMemory = _config.CaptureMemoryStats;
        var captureTerminal = _config.CaptureTerminalStats;
        var snapshot = new DiagnosticsSnapshot(
            System.DateTimeOffset.UtcNow,
            captureMemory ? System.GC.GetTotalMemory(false) : 0,
            captureMemory ? System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 : 0,
            captureMemory ? System.Diagnostics.Process.GetCurrentProcess().Threads.Count : 0,
            captureMemory ? System.GC.CollectionCount(0) : 0,
            captureMemory ? System.GC.CollectionCount(1) : 0,
            captureMemory ? System.GC.CollectionCount(2) : 0,
            activeNooks,
            activeBays,
            activeAgents,
            0,
            captureTerminal ? (nookScrollbackBytes ?? new()) : new());

        if (_config.Enabled)
        {
            lock (_snapshots)
            {
                _snapshots.Add(snapshot);
                while (_snapshots.Count > _config.MaxSnapshots)
                    _snapshots.RemoveAt(0);
            }
        }

        return snapshot;
    }

    public System.Collections.Generic.IReadOnlyList<DiagnosticsSnapshot> GetSnapshots()
    {
        lock (_snapshots)
            return _snapshots.ToList();
    }

    public string ExportSnapshotJson(DiagnosticsSnapshot snapshot)
    {
        return JsonSerializer.Serialize(snapshot, DiagnosticsJsonContext.Default.DiagnosticsSnapshot);
    }

    public string ExportAllSnapshotsJson()
    {
        lock (_snapshots)
        {
            var list = _snapshots.ToList();
            return JsonSerializer.Serialize(list, DiagnosticsJsonContext.Default.ListDiagnosticsSnapshot);
        }
    }

    private void StartSnapshotLoop()
    {
        _cts = new System.Threading.CancellationTokenSource();
        _snapshotTask = SnapshotLoopAsync(_cts.Token);
        _logger.LogInformation("diagnostics: snapshot loop started (interval={interval})", _config.SnapshotInterval);
    }

    private void StopSnapshotLoop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _snapshotTask = null;
        _logger.LogInformation("diagnostics: snapshot loop stopped");
    }

    private async Task SnapshotLoopAsync(System.Threading.CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_config.SnapshotInterval, ct).ConfigureAwait(false);
                TakeSnapshot();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "diagnostics: snapshot loop error");
            }
        }
    }
}
