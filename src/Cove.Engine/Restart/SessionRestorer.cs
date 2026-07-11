using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Restart;

public sealed record RestorableNook(
    string NookId,
    string Command,
    string[] Args,
    string Cwd,
    string? Title,
    string? Adapter,
    string? AgentName,
    string? SessionId,
    bool Yolo);

public sealed record RestoreSummary(int Restored, int Fresh, int Skipped);

public sealed record RestorationSummaryEvent(int Restored, int Fresh, int Skipped, string BootedAt = "");

public sealed record RestoreSummaryPullResult(bool Present, int Restored, int Fresh, int Skipped, string BootedAt);

public interface IRestoreSpawner
{
    void Respawn(RestorableNook nook, string command, string[] args, string cwd);
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

    public RestoreSummary Restore(IReadOnlyList<RestorableNook?> nooks, bool enabled)
    {
        if (!enabled)
        {
            _logger.LogWarning("session restoration disabled by config, {Count} nooks left dead", nooks.Count);
            return new RestoreSummary(0, 0, 0);
        }

        int restored = 0, fresh = 0, skipped = 0;
        foreach (var nook in nooks)
        {
            if (nook is null || (string.IsNullOrEmpty(nook.Command) && string.IsNullOrEmpty(nook.Adapter)))
            {
                _logger.LogWarning("skipping restore of nook with no usable record");
                skipped++;
                continue;
            }

            if (!string.IsNullOrEmpty(nook.Adapter))
            {
                if (!string.IsNullOrEmpty(nook.SessionId) && TryResume(nook, out var command, out var args, out var cwd))
                {
                    _spawner.Respawn(nook, command, args, cwd);
                    restored++;
                    continue;
                }

                _logger.LogWarning("agent nook {NookId} adapter {Adapter} could not resume, starting fresh", nook.NookId, nook.Adapter);
                _spawner.Respawn(nook, nook.Command, nook.Args, nook.Cwd);
                fresh++;
                continue;
            }

            _spawner.Respawn(nook, nook.Command, nook.Args, nook.Cwd);
            restored++;
        }

        return new RestoreSummary(restored, fresh, skipped);
    }

    private bool TryResume(RestorableNook nook, out string command, out string[] args, out string cwd)
    {
        command = "";
        args = System.Array.Empty<string>();
        cwd = nook.Cwd;
        try
        {
            var overrides = new LauncherOverrides { WorkingDir = nook.Cwd, Yolo = nook.Yolo };
            var rc = _buildResume(nook.Adapter!, nook.SessionId!, overrides);
            command = rc.Command;
            args = System.Linq.Enumerable.ToArray(rc.Args);
            cwd = string.IsNullOrEmpty(rc.Cwd) ? nook.Cwd : rc.Cwd;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "resume command build failed for {NookId}", nook.NookId);
            return false;
        }
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RestorationSummaryEvent))]
[JsonSerializable(typeof(RestoreSummaryPullResult))]
public sealed partial class RestorationSummaryJsonContext : JsonSerializerContext { }
