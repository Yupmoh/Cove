using System.Text.Json.Serialization;
using Cove.Adapters;
using Cove.Engine.Restart;

namespace Cove.Engine.Launch;

public sealed record LaunchFlags(
    [property: JsonPropertyName("dangerouslySkipPermissions")] bool DangerouslySkipPermissions,
    [property: JsonPropertyName("worktreePath")] string? WorktreePath,
    [property: JsonPropertyName("extra")] IReadOnlyDictionary<string, string> Extra,
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("effort")] string? Effort,
    [property: JsonPropertyName("extraArgs")] string? ExtraArgs,
    [property: JsonPropertyName("provider")] string? Provider);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LaunchFlags))]
public sealed partial class LaunchFlagsJsonContext : JsonSerializerContext { }

public sealed class LaunchOrchestrator
{
    private readonly AgentResumeService? _resumeService;
    private readonly AdapterManifestStore? _manifestStore;
    private readonly MethodRunner? _methodRunner;
    private readonly BinaryDiscoveryService? _binaryDiscovery;
    private readonly string? _loginShellPath;
    private readonly Dictionary<string, LauncherOverrides> _paneOverrides = new();

    public LaunchOrchestrator(AgentResumeService? resumeService = null)
    {
        _resumeService = resumeService;
    }

    public LaunchOrchestrator(AdapterManifestStore manifestStore, MethodRunner methodRunner, BinaryDiscoveryService binaryDiscovery, string? loginShellPath = null, AgentResumeService? resumeService = null)
    {
        _manifestStore = manifestStore;
        _methodRunner = methodRunner;
        _binaryDiscovery = binaryDiscovery;
        _loginShellPath = loginShellPath;
        _resumeService = resumeService;
    }

    public ResumeCommand BuildLaunchCommand(LaunchProfile profile, LauncherOverrides overrides)
    {
        var binary = profile.CliArgs.Count > 0 ? profile.CliArgs[0] : "agent";
        var args = new List<string>(profile.CliArgs.Skip(1));
        ApplyOverrides(args, overrides);
        return new ResumeCommand(binary, args, overrides.WorkingDir ?? "");
    }

    public async Task<ResumeCommand> BuildLaunchCommandAsync(LaunchProfile profile, LauncherOverrides overrides, System.Threading.CancellationToken cancellationToken = default)
    {
        if (_manifestStore is null || _methodRunner is null || _binaryDiscovery is null)
            return BuildLaunchCommand(profile, overrides);

        var manifest = _manifestStore.Load(profile.Adapter);
        if (manifest is null)
            throw new ResumeFailedException($"unknown adapter: {profile.Adapter}");

        var adapterDir = _manifestStore.ResolveDir(profile.Adapter);
        var flagsJson = BuildFlagsJson(profile, overrides);

        if (manifest.Methods.TryGetValue("build_launch_command", out var method) && method.Script is not null)
        {
            var result = await _methodRunner.RunAsync(adapterDir, method.Script, new[] { flagsJson }, TimeSpan.FromSeconds(5), cancellationToken: cancellationToken).ConfigureAwait(false);
            if (result.Ok && result.Json is { } json)
                return ParseCommand(json, overrides.WorkingDir);
            if (result.GracefulFailure)
                throw new ResumeFailedException($"adapter {profile.Adapter} cannot build launch command: {result.Stderr}");
            throw new ResumeFailedException($"adapter {profile.Adapter} build_launch_command failed (exit {result.ExitCode}): {result.Stderr}");
        }

        var binaryPath = await ResolveBinaryAsync(manifest, adapterDir, cancellationToken).ConfigureAwait(false);
        if (binaryPath is null)
            throw new ResumeFailedException($"binary not found for adapter {profile.Adapter}");

        return BuildFallbackCommand(binaryPath, profile, overrides);
    }

    public async Task<AgentResumeResult> ResumeAsync(LaunchProfile profile, string sessionId, LauncherOverrides overrides, System.Threading.CancellationToken cancellationToken = default)
    {
        if (_resumeService is not null)
            return await _resumeService.ResumeAsync(sessionId, overrides, cancellationToken).ConfigureAwait(false);

        var cmd = BuildLaunchCommand(profile, overrides);
        return new AgentResumeResult(AgentResumeState.Succeeded, cmd, null, sessionId);
    }

    private async Task<string?> ResolveBinaryAsync(AdapterManifest manifest, string adapterDir, System.Threading.CancellationToken cancellationToken)
    {
        if (manifest.BinaryDiscovery is { } discovery)
        {
            var result = _binaryDiscovery!.Discover(discovery, manifest.WellKnownPaths, _loginShellPath);
            if (result.State == AdapterDetectionState.Detected && !string.IsNullOrEmpty(result.BinaryPath))
                return result.BinaryPath;
            return null;
        }

        if (manifest.Methods.TryGetValue("detect_binary", out var method) && method.Script is not null && _methodRunner is not null)
        {
            var result = await _methodRunner.RunAsync(adapterDir, method.Script, Array.Empty<string>(), TimeSpan.FromSeconds(5), cancellationToken: cancellationToken).ConfigureAwait(false);
            if (result.Ok && result.Json is { } json && json.TryGetProperty("path", out var pathProp))
            {
                var path = pathProp.GetString();
                return string.IsNullOrEmpty(path) ? null : path;
            }
        }

        return null;
    }

    private static ResumeCommand ParseCommand(System.Text.Json.JsonElement json, string? workingDir)
    {
        if (!json.TryGetProperty("command", out var cmdProp) || cmdProp.ValueKind != System.Text.Json.JsonValueKind.Array)
            throw new ResumeFailedException("build_launch_command returned invalid command payload");
        var parts = new List<string>();
        foreach (var item in cmdProp.EnumerateArray())
        {
            var s = item.GetString();
            if (s is not null)
                parts.Add(s);
        }
        if (parts.Count == 0)
            throw new ResumeFailedException("build_launch_command returned empty command array");
        return new ResumeCommand(parts[0], parts.Skip(1).ToArray(), workingDir ?? "");
    }

    private static ResumeCommand BuildFallbackCommand(string binary, LaunchProfile profile, LauncherOverrides overrides)
    {
        var args = new List<string>(profile.CliArgs.Count > 0 ? profile.CliArgs.Skip(1) : Array.Empty<string>());
        ApplyOverrides(args, overrides);
        return new ResumeCommand(binary, args, overrides.WorkingDir ?? "");
    }

    private static string BuildFlagsJson(LaunchProfile profile, LauncherOverrides overrides)
    {
        var extra = new Dictionary<string, string>();
        var argParts = new List<string>(profile.CliArgs.Count > 0 ? profile.CliArgs.Skip(1) : Array.Empty<string>());
        argParts.AddRange(overrides.ExtraFlags);
        var extraArgs = argParts.Count > 0 ? string.Join(" ", argParts) : null;
        var flags = new LaunchFlags(overrides.Yolo, null, extra, profile.Model, profile.Effort, extraArgs, null);
        return System.Text.Json.JsonSerializer.Serialize(flags, LaunchFlagsJsonContext.Default.LaunchFlags);
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
