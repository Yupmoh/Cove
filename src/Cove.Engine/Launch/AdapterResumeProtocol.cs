using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Adapters;
using Cove.Engine.Restart;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Launch;

public sealed class AdapterResumeProtocol : IAdapterResume
{
    private readonly AdapterManifestStore _manifestStore;
    private readonly MethodRunner _methodRunner;
    private readonly ILogger? _logger;
    private readonly ILaunchAdapterLookup? _adapters;
    private readonly ILaunchProcessAcquirer? _processes;

    public AdapterResumeProtocol(
        AdapterManifestStore manifestStore,
        MethodRunner methodRunner,
        ILogger? logger = null,
        ILaunchAdapterLookup? adapters = null,
        ILaunchProcessAcquirer? processes = null)
    {
        if ((adapters is null) != (processes is null))
            throw new ArgumentException("adapter lookup and process acquisition must be configured together");
        _manifestStore = manifestStore;
        _methodRunner = methodRunner;
        _logger = logger;
        _adapters = adapters;
        _processes = processes;
    }

    public async Task<ResumeCommand> BuildResumeCommandAsync(string adapter, string sessionId, LauncherOverrides overrides, System.Threading.CancellationToken cancellationToken = default)
    {
        var manifest = _manifestStore.Load(adapter);
        if (manifest is null)
            throw new ResumeFailedException($"unknown adapter: {adapter}");

        if (OperatingSystem.IsWindows() && _adapters?.Find(adapter) is { } launchAdapter && _processes is { } processes)
        {
            var binary = await processes.AcquireBinaryAsync(launchAdapter, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(binary)
                && WindowsAdapterResumeCommand.Build(adapter, binary, _manifestStore.ResolveDir(adapter), sessionId, overrides) is { } nativeCommand)
                return nativeCommand;
        }

        if (!manifest.Methods.TryGetValue("build_resume_command", out var method) || method.Script is null)
            throw new ResumeFailedException($"adapter {adapter} has no build_resume_command method");
        var adapterDir = _manifestStore.ResolveDir(adapter);
        var flagsJson = BuildResumeFlagsJson(sessionId, overrides);

        var result = await _methodRunner.RunAsync(adapterDir, method.Script, new[] { sessionId, flagsJson }, TimeSpan.FromSeconds(5), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.Ok && result.Json is { } json)
            return ParseCommand(json, overrides.WorkingDir);
        if (result.GracefulFailure)
            throw new ResumeFailedException($"adapter {adapter} cannot build resume command: {result.Stderr}");
        throw new ResumeFailedException($"adapter {adapter} build_resume_command failed (exit {result.ExitCode}): {result.Stderr}");
    }

    public Task WaitForReadiness(string sessionId, System.Threading.CancellationToken cancellationToken)
        => Task.CompletedTask;

    public bool IsSessionReaped(string sessionId) => false;

    private static string BuildResumeFlagsJson(string sessionId, LauncherOverrides overrides)
    {
        var extra = new Dictionary<string, string>();
        var argParts = new List<string>(overrides.ExtraFlags);
        var extraArgs = argParts.Count > 0 ? string.Join(" ", argParts) : null;
        var flags = new ResumeFlags(sessionId, overrides.Yolo, overrides.WorkingDir, extra, extraArgs);
        return JsonSerializer.Serialize(flags, ResumeFlagsJsonContext.Default.ResumeFlags);
    }

    private static ResumeCommand ParseCommand(System.Text.Json.JsonElement json, string? workingDir)
    {
        if (!json.TryGetProperty("command", out var cmdProp) || cmdProp.ValueKind != System.Text.Json.JsonValueKind.Array)
            throw new ResumeFailedException("build_resume_command returned invalid command payload");
        var parts = new List<string>();
        foreach (var item in cmdProp.EnumerateArray())
        {
            var s = item.GetString();
            if (s is not null)
                parts.Add(s);
        }
        if (parts.Count == 0)
            throw new ResumeFailedException("build_resume_command returned empty command array");
        return new ResumeCommand(parts[0], parts.Skip(1).ToArray(), workingDir ?? "");
    }
}

public sealed record ResumeFlags(
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("dangerouslySkipPermissions")] bool DangerouslySkipPermissions,
    [property: JsonPropertyName("worktreePath")] string? WorktreePath,
    [property: JsonPropertyName("extra")] IReadOnlyDictionary<string, string> Extra,
    [property: JsonPropertyName("extraArgs")] string? ExtraArgs);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ResumeFlags))]
public sealed partial class ResumeFlagsJsonContext : JsonSerializerContext { }
