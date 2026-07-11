using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Restart;

public sealed record RestorablePane(
    string PaneId,
    string Command,
    string[] Args,
    string Cwd,
    string? Title,
    string? Adapter,
    string? AgentName,
    string? SessionId,
    bool Yolo);

public sealed record RestoreSummary(int Restored, int Fresh, int Skipped);

public sealed record RestorationSummaryEvent(int Restored, int Fresh, int Skipped);

public interface IRestoreSpawner
{
    void Respawn(RestorablePane pane, string command, string[] args, string cwd);
}

public sealed class SessionRestorer
{
    private readonly IRestoreSpawner _spawner;
    private readonly Func<string, string, LauncherOverrides, ResumeCommand> _buildResume;
    private readonly ILogger _logger;

    public SessionRestorer(IRestoreSpawner spawner, Func<string, string, LauncherOverrides, ResumeCommand> buildResume, ILogger logger)
    {
        _spawner = spawner;
        _buildResume = buildResume;
        _logger = logger;
    }

    public RestoreSummary Restore(IReadOnlyList<RestorablePane?> panes, bool enabled)
    {
        if (!enabled)
        {
            _logger.LogWarning("session restoration disabled by config, {Count} panes left dead", panes.Count);
            return new RestoreSummary(0, 0, 0);
        }

        int restored = 0, fresh = 0, skipped = 0;
        foreach (var pane in panes)
        {
            if (pane is null || (string.IsNullOrEmpty(pane.Command) && string.IsNullOrEmpty(pane.Adapter)))
            {
                _logger.LogWarning("skipping restore of pane with no usable record");
                skipped++;
                continue;
            }

            if (!string.IsNullOrEmpty(pane.Adapter))
            {
                if (!string.IsNullOrEmpty(pane.SessionId) && TryResume(pane, out var command, out var args, out var cwd))
                {
                    _spawner.Respawn(pane, command, args, cwd);
                    restored++;
                    continue;
                }

                _logger.LogWarning("agent pane {PaneId} adapter {Adapter} could not resume, starting fresh", pane.PaneId, pane.Adapter);
                _spawner.Respawn(pane, pane.Command, pane.Args, pane.Cwd);
                fresh++;
                continue;
            }

            _spawner.Respawn(pane, pane.Command, pane.Args, pane.Cwd);
            restored++;
        }

        return new RestoreSummary(restored, fresh, skipped);
    }

    private bool TryResume(RestorablePane pane, out string command, out string[] args, out string cwd)
    {
        command = "";
        args = System.Array.Empty<string>();
        cwd = pane.Cwd;
        try
        {
            var overrides = new LauncherOverrides { WorkingDir = pane.Cwd, Yolo = pane.Yolo };
            var rc = _buildResume(pane.Adapter!, pane.SessionId!, overrides);
            command = rc.Command;
            args = System.Linq.Enumerable.ToArray(rc.Args);
            cwd = string.IsNullOrEmpty(rc.Cwd) ? pane.Cwd : rc.Cwd;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "resume command build failed for {PaneId}", pane.PaneId);
            return false;
        }
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RestorationSummaryEvent))]
public sealed partial class RestorationSummaryJsonContext : JsonSerializerContext { }
