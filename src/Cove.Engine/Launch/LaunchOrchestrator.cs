using System.Text.Json.Serialization;
using Cove.Adapters;
using Cove.Engine.Restart;
using Microsoft.Extensions.Logging;
using ZLogger;

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
    private readonly Dictionary<string, LauncherOverrides> _nookOverrides = new();
    private readonly LauncherOverrideStore? _overrideStore;
    private readonly Microsoft.Extensions.Logging.ILogger? _logger;

    public LaunchOrchestrator(AgentResumeService? resumeService = null, LauncherOverrideStore? overrideStore = null)
    {
        _resumeService = resumeService;
        _overrideStore = overrideStore;
    }
    public LaunchOrchestrator(AdapterManifestStore manifestStore, MethodRunner methodRunner, BinaryDiscoveryService binaryDiscovery, string? loginShellPath = null, AgentResumeService? resumeService = null, LauncherOverrideStore? overrideStore = null, Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        _manifestStore = manifestStore;
        _methodRunner = methodRunner;
        _binaryDiscovery = binaryDiscovery;
        _loginShellPath = loginShellPath;
        _resumeService = resumeService;
        _overrideStore = overrideStore;
        _logger = logger;
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

    public BinaryDiscoveryResult DescribeAdapterBinary(AdapterManifest manifest)
    {
        if (_binaryDiscovery is null || manifest.BinaryDiscovery is not { } discovery)
        {
            _logger?.AdapterBinaryDiscoveryUnavailable(manifest.Name);
            return new BinaryDiscoveryResult(AdapterDetectionState.Missing, null, null);
        }
        return _binaryDiscovery.Discover(discovery, manifest.WellKnownPaths, _loginShellPath);
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
    public async Task<LauncherOptionsResult?> LoadLauncherOptionsAsync(string adapter, System.Threading.CancellationToken cancellationToken = default)
    {
        if (_manifestStore is null)
            return null;
        var manifest = _manifestStore.Load(adapter);
        if (manifest is null)
            return null;
        if (!manifest.Methods.TryGetValue("launcher_options", out var method))
            return null;
        var adapterDir = _manifestStore.ResolveDir(adapter);
        System.Text.Json.JsonElement output;
        if (!string.IsNullOrEmpty(method.Static))
        {
            var staticPath = System.IO.Path.Combine(adapterDir, method.Static);
            if (!System.IO.File.Exists(staticPath))
            {
                _logger?.LauncherOptionsStaticMissing(adapter, method.Static);
                return null;
            }
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(staticPath));
                output = doc.RootElement.Clone();
            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger?.LauncherOptionsParseFailed(adapter, ex.Message);
                return null;
            }
        }
        else if (!string.IsNullOrEmpty(method.Script) && _methodRunner is not null)
        {
            var result = await _methodRunner.RunAsync(adapterDir, method.Script, Array.Empty<string>(), TimeSpan.FromSeconds(5), cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!result.Ok || result.Json is not { } json)
            {
                _logger?.LauncherOptionsScriptFailed(adapter, result.ExitCode, result.Stderr);
                return null;
            }
            output = json;
        }
        else
        {
            return null;
        }
        return ParseLauncherOptions(output);
    }
    private static LauncherOptionsResult? ParseLauncherOptions(System.Text.Json.JsonElement json)
    {
        if (!json.TryGetProperty("options", out var optsProp) || optsProp.ValueKind != System.Text.Json.JsonValueKind.Array)
            return null;
        var options = new List<LauncherOption>();
        foreach (var item in optsProp.EnumerateArray())
        {
            if (!item.TryGetProperty("key", out var keyProp) || !item.TryGetProperty("label", out var labelProp) || !item.TryGetProperty("type", out var typeProp))
                continue;
            string? defaultValueRaw = null;
            if (item.TryGetProperty("default", out var defProp) && defProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                defaultValueRaw = defProp.ValueKind == System.Text.Json.JsonValueKind.String ? defProp.GetString() : defProp.GetRawText();
            List<LauncherOptionChoice>? choices = null;
            if (item.TryGetProperty("choices", out var choicesProp) && choicesProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                choices = new List<LauncherOptionChoice>();
                foreach (var c in choicesProp.EnumerateArray())
                {
                    if (c.ValueKind == System.Text.Json.JsonValueKind.String)
                        choices.Add(new LauncherOptionChoice(c.GetString()!, null));
                    else if (c.TryGetProperty("value", out var valProp))
                        choices.Add(new LauncherOptionChoice(valProp.GetString()!, c.TryGetProperty("label", out var lblProp) ? lblProp.GetString() : null));
                }
            }
            options.Add(new LauncherOption(keyProp.GetString()!, labelProp.GetString()!, typeProp.GetString()!, defaultValueRaw, choices));
        }
        return new LauncherOptionsResult(options);
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

    public void PersistOverrides(string nookId, LauncherOverrides overrides)
    {
        _nookOverrides[nookId] = overrides;
        _overrideStore?.Save(nookId, overrides);
    }

    public LauncherOverrides? GetOverrides(string nookId)
    {
        if (_nookOverrides.TryGetValue(nookId, out var overrides))
            return overrides;
        if (_overrideStore is not null && _overrideStore.TryLoad(nookId, out var loaded) && loaded is not null)
        {
            _nookOverrides[nookId] = loaded;
            return loaded;
        }
        return null;
    }

    public void ClearOverrides(string nookId)
    {
        _nookOverrides.Remove(nookId);
        _overrideStore?.Delete(nookId);
    }
}

internal static partial class LauncherOptionsLog
{
    [ZLoggerMessage(LogLevel.Warning, "launcher options static file missing adapter={adapter} file={file}")]
    public static partial void LauncherOptionsStaticMissing(this Microsoft.Extensions.Logging.ILogger logger, string adapter, string file);

    [ZLoggerMessage(LogLevel.Warning, "launcher options parse failed adapter={adapter} error={error}")]
    public static partial void LauncherOptionsParseFailed(this Microsoft.Extensions.Logging.ILogger logger, string adapter, string error);

    [ZLoggerMessage(LogLevel.Warning, "launcher options script failed adapter={adapter} exit={exit} stderr={stderr}")]
    public static partial void LauncherOptionsScriptFailed(this Microsoft.Extensions.Logging.ILogger logger, string adapter, int exit, string stderr);

    [ZLoggerMessage(LogLevel.Warning, "adapter binary discovery unavailable adapter={adapter}")]
    public static partial void AdapterBinaryDiscoveryUnavailable(this Microsoft.Extensions.Logging.ILogger logger, string adapter);
}
