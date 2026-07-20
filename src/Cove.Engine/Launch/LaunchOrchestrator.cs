using Cove.Adapters;
using Cove.Engine.Restart;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Launch;

public sealed class LaunchOrchestrator
{
    private readonly LaunchCommandComposer _commands;
    private readonly ILaunchAdapterLookup? _adapters;
    private readonly ILaunchProcessAcquirer? _processes;
    private readonly ILauncherOptionsResolver? _launcherOptions;
    private readonly ILaunchProfileLookup? _profiles;
    private readonly AgentResumeService? _resumeService;
    private readonly LauncherOverrideStore? _overrideStore;
    private readonly ILogger? _logger;
    private readonly Dictionary<string, LauncherOverrides> _nookOverrides = new();

    public LaunchOrchestrator(
        LaunchCommandComposer commands,
        ILaunchAdapterLookup? adapters = null,
        ILaunchProcessAcquirer? processes = null,
        ILauncherOptionsResolver? launcherOptions = null,
        ILaunchProfileLookup? profiles = null,
        AgentResumeService? resumeService = null,
        LauncherOverrideStore? overrideStore = null,
        ILogger? logger = null)
    {
        if ((adapters is null) != (processes is null))
        {
            throw new ArgumentException(
                "adapter lookup and process acquisition must be configured together");
        }

        _commands = commands;
        _adapters = adapters;
        _processes = processes;
        _launcherOptions = launcherOptions;
        _profiles = profiles;
        _resumeService = resumeService;
        _overrideStore = overrideStore;
        _logger = logger;
    }

    public bool CanResolveProfiles => _profiles is not null;

    public LaunchProfile? FindProfile(string adapter, string profileSlug)
        => _profiles?.Find(adapter, profileSlug);

    public LaunchProfile? ResolveProfile(string adapter)
        => _profiles?.Resolve(adapter);

    public ResumeCommand BuildLaunchCommand(
        LaunchProfile profile,
        LauncherOverrides overrides)
        => _commands.Compose(profile, overrides);

    public async Task<ResumeCommand> BuildLaunchCommandAsync(
        LaunchProfile profile,
        LauncherOverrides overrides,
        CancellationToken cancellationToken = default)
    {
        if (_adapters is null || _processes is null)
            return _commands.Compose(profile, overrides);

        var adapter = _adapters.Find(profile.Adapter);
        if (adapter is null)
            throw new ResumeFailedException($"unknown adapter: {profile.Adapter}");

        if (OperatingSystem.IsWindows())
        {
            var nativeBinary = await _processes.AcquireBinaryAsync(adapter, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(nativeBinary)
                && WindowsAdapterLaunchCommand.Build(nativeBinary, adapter.Directory, profile, overrides) is { } nativeCommand)
            {
                LogComposed(profile.Adapter, nativeCommand);
                return nativeCommand;
            }
        }

        if (adapter.Manifest.Methods.TryGetValue(
                "build_launch_command",
                out var method)
            && method.Script is not null)
        {
            _logger?.LaunchBuildCommand(
                profile.Adapter,
                "build_launch_command");
            var result = await _processes.RunMethodAsync(
                adapter,
                "build_launch_command",
                method.Script,
                [_commands.BuildFlagsJson(profile, overrides)],
                cancellationToken).ConfigureAwait(false);
            if (result.Ok && result.Json is { } json)
            {
                var composed = _commands.ParseAdapterCommand(
                    json,
                    overrides.WorkingDir);
                LogComposed(profile.Adapter, composed);
                return composed;
            }

            if (result.GracefulFailure)
            {
                throw new ResumeFailedException(
                    $"adapter {profile.Adapter} cannot build launch command: {result.Stderr}");
            }

            throw new ResumeFailedException(
                $"adapter {profile.Adapter} build_launch_command failed (exit {result.ExitCode}): {result.Stderr}");
        }

        _logger?.LaunchBuildCommand(profile.Adapter, "fallback");
        var binary = await _processes.AcquireBinaryAsync(
            adapter,
            cancellationToken).ConfigureAwait(false);
        if (binary is null)
        {
            throw new ResumeFailedException(
                $"binary not found for adapter {profile.Adapter}");
        }

        var fallback = _commands.Compose(binary, profile, overrides);
        if (OperatingSystem.IsWindows())
            fallback = WindowsAdapterLaunchCommand.WrapCommandShim(fallback);
        LogComposed(profile.Adapter, fallback);
        return fallback;
    }

    public async Task<AgentResumeResult> ResumeAsync(
        LaunchProfile profile,
        string sessionId,
        LauncherOverrides overrides,
        CancellationToken cancellationToken = default)
    {
        if (_resumeService is not null)
        {
            return await _resumeService.ResumeAsync(
                profile.Adapter,
                sessionId,
                overrides,
                cancellationToken).ConfigureAwait(false);
        }

        var command = _commands.Compose(profile, overrides);
        return new AgentResumeResult(
            AgentResumeState.Succeeded,
            command,
            null,
            sessionId);
    }

    public void RefreshLoginShellPath()
    {
        _processes?.RefreshLoginShellPath();
    }

    public BinaryDiscoveryResult DescribeAdapterBinary(
        AdapterManifest manifest)
    {
        if (_processes is not null)
            return _processes.Describe(manifest);

        _logger?.AdapterBinaryDiscoveryUnavailable(manifest.Name);
        return new BinaryDiscoveryResult(
            AdapterDetectionState.Missing,
            null,
            null);
    }

    public Task<LauncherOptionsResult?> LoadLauncherOptionsAsync(
        string adapter,
        CancellationToken cancellationToken = default)
        => _launcherOptions is null
            ? Task.FromResult<LauncherOptionsResult?>(null)
            : _launcherOptions.LoadAsync(adapter, cancellationToken);

    public void PersistOverrides(
        string nookId,
        LauncherOverrides overrides)
    {
        _nookOverrides[nookId] = overrides;
        _overrideStore?.Save(nookId, overrides);
    }

    public LauncherOverrides? GetOverrides(string nookId)
    {
        if (_nookOverrides.TryGetValue(nookId, out var overrides))
            return overrides;
        if (_overrideStore is not null
            && _overrideStore.TryLoad(nookId, out var loaded)
            && loaded is not null)
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

    private void LogComposed(
        string adapter,
        ResumeCommand command)
    {
        _logger?.LaunchCommandComposed(
            adapter,
            command.Command,
            command.Args.Count(),
            command.Cwd);
    }
}

internal static partial class LauncherOptionsLog
{
    [ZLoggerMessage(LogLevel.Warning, "launcher options static file missing adapter={adapter} file={file}")]
    public static partial void LauncherOptionsStaticMissing(
        this ILogger logger,
        string adapter,
        string file);

    [ZLoggerMessage(LogLevel.Warning, "launcher options parse failed adapter={adapter} error={error}")]
    public static partial void LauncherOptionsParseFailed(
        this ILogger logger,
        string adapter,
        string error);

    [ZLoggerMessage(LogLevel.Warning, "launcher options script failed adapter={adapter} exit={exit} stderr={stderr}")]
    public static partial void LauncherOptionsScriptFailed(
        this ILogger logger,
        string adapter,
        int exit,
        string stderr);

    [ZLoggerMessage(LogLevel.Warning, "adapter binary discovery unavailable adapter={adapter}")]
    public static partial void AdapterBinaryDiscoveryUnavailable(
        this ILogger logger,
        string adapter);

    [ZLoggerMessage(3200, LogLevel.Debug, "launch build command adapter={adapter} route={route}")]
    public static partial void LaunchBuildCommand(
        this ILogger logger,
        string adapter,
        string route);

    [ZLoggerMessage(3201, LogLevel.Debug, "launch method runner adapter={adapter} method={method} durationMs={durationMs} exit={exit} ok={ok}")]
    public static partial void LaunchMethodRunner(
        this ILogger logger,
        string adapter,
        string method,
        double durationMs,
        int exit,
        bool ok);

    [ZLoggerMessage(3202, LogLevel.Debug, "launch binary discovery adapter={adapter} state={state} path={path}")]
    public static partial void LaunchBinaryDiscovery(
        this ILogger logger,
        string adapter,
        string state,
        string path);

    [ZLoggerMessage(3203, LogLevel.Debug, "launch command composed adapter={adapter} binary={binary} argCount={argCount} cwd={cwd}")]
    public static partial void LaunchCommandComposed(
        this ILogger logger,
        string adapter,
        string binary,
        int argCount,
        string cwd);
}
