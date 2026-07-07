using Cove.Adapters;
using Cove.Engine.Restart;

namespace Cove.Engine.Launch;

public sealed class LaunchOrchestrator
{
    private readonly AgentResumeService? _resumeService;
    private readonly Dictionary<string, LauncherOverrides> _paneOverrides = new();

    public LaunchOrchestrator(AgentResumeService? resumeService = null)
    {
        _resumeService = resumeService;
    }

    public ResumeCommand BuildLaunchCommand(LaunchProfile profile, LauncherOverrides overrides)
    {
        var binary = profile.CliArgs.Count > 0 ? profile.CliArgs[0] : "agent";
        var args = new List<string>(profile.CliArgs.Skip(1));
        ApplyOverrides(args, overrides);
        return new ResumeCommand(binary, args, overrides.WorkingDir ?? "");
    }

    public async Task<AgentResumeResult> ResumeAsync(LaunchProfile profile, string sessionId, LauncherOverrides overrides, System.Threading.CancellationToken cancellationToken = default)
    {
        if (_resumeService is not null)
            return await _resumeService.ResumeAsync(sessionId, overrides, cancellationToken).ConfigureAwait(false);

        var cmd = BuildLaunchCommand(profile, overrides);
        return new AgentResumeResult(AgentResumeState.Succeeded, cmd, null, sessionId);
    }

    private static void ApplyOverrides(List<string> args, LauncherOverrides overrides)
    {
        if (overrides.Yolo)
            args.Add("--dangerously-skip-permissions");
        foreach (var flag in overrides.ExtraFlags)
            args.Add(flag);
        foreach (var env in overrides.Env)
            args.Add($"--env={env.Key}={env.Value}");
    }

    public void PersistOverrides(string paneId, LauncherOverrides overrides)
    {
        _paneOverrides[paneId] = overrides;
    }

    public LauncherOverrides? GetOverrides(string paneId)
    {
        return _paneOverrides.TryGetValue(paneId, out var overrides) ? overrides : null;
    }

    public void ClearOverrides(string paneId)
    {
        _paneOverrides.Remove(paneId);
    }
}
