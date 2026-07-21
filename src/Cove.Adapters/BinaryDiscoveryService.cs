using System.Text.RegularExpressions;
using Cove.Platform;
using Microsoft.Extensions.Logging;

namespace Cove.Adapters;

public sealed record BinaryDiscoveryResult(
    AdapterDetectionState State,
    string? BinaryPath,
    string? Version);

public sealed class BinaryDiscoveryService
{
    private static readonly Regex DefaultVersionRegex = new(@"(\d+\.\d+\.\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly string[] WindowsExecutableExtensions = [".exe", ".com", ".cmd", ".bat"];
    private readonly ILogger? _logger;
    private readonly IPlatformFileSystem _fileSystem;
    private readonly IProcessRunner _processRunner;
    private readonly IRuntimeEnvironment _environment;
    private string? _cachedPath;

    public BinaryDiscoveryService(
        ILogger? logger = null,
        IPlatformFileSystem? fileSystem = null,
        IProcessRunner? processRunner = null,
        IRuntimeEnvironment? environment = null)
    {
        _logger = logger;
        _fileSystem = fileSystem ?? SystemPlatformFileSystem.Instance;
        _processRunner = processRunner ?? new SystemProcessRunner();
        _environment = environment ?? SystemRuntimeEnvironment.Instance;
    }

    public BinaryDiscoveryResult Discover(BinaryDiscovery config, string? loginShellPath = null)
    {
        var source = loginShellPath is not null ? "login-shell" : "process-env";
        var rawPath = loginShellPath ?? _environment.ExecutablePath ?? "";
        var pathDirs = ResolvePathDirs(rawPath);
        var commands = string.Join(",", config.Commands);

        _logger?.BinaryProbeStarted(commands, source, pathDirs.Count, config.WellKnownPaths.Count);
        _logger?.BinaryProbePathContents(source, rawPath);

        foreach (var command in config.Commands)
            foreach (var directory in pathDirs)
                if (TryCommand(command, directory, config, "path", rawPath) is { } found)
                    return found;

        foreach (var directory in config.WellKnownPaths)
        {
            var expanded = ExpandTilde(directory);
            foreach (var command in config.Commands)
                if (TryCommand(command, expanded, config, "well-known", rawPath) is { } found)
                    return found;
        }

        _logger?.BinaryProbeMissing(commands, pathDirs.Count, config.WellKnownPaths.Count);
        return new BinaryDiscoveryResult(AdapterDetectionState.Missing, null, null);
    }

    private BinaryDiscoveryResult? TryCommand(string command, string directory, BinaryDiscovery config, string source, string executablePath)
    {
        if (_environment.IsWindows && !Path.HasExtension(command))
        {
            foreach (var extension in WindowsExecutableExtensions)
                if (TryCandidate(command, Path.Combine(directory, command + extension), config, source, executablePath) is { } found)
                    return found;
            return null;
        }
        if (TryCandidate(command, Path.Combine(directory, command), config, source, executablePath) is { } exact)
            return exact;
        return null;
    }

    private BinaryDiscoveryResult? TryCandidate(string command, string candidate, BinaryDiscovery config, string source, string executablePath)
    {
        var exists = _fileSystem.FileExists(candidate);
        _logger?.BinaryCandidateTested(command, candidate, exists, source);
        if (!exists)
            return null;
        _logger?.BinaryResolved(command, candidate, source);
        if (_environment.IsWindows && IsPosixStylePath(candidate))
            _logger?.BinaryResolvedNonRunnableWindowsPath(command, candidate);
        return ProbeVersion(candidate, config, executablePath);
    }

    private BinaryDiscoveryResult ProbeVersion(string binaryPath, BinaryDiscovery config, string executablePath)
    {
        string? version = null;
        if (!string.IsNullOrEmpty(config.VersionFlag))
        {
            try
            {
                var isCommandShim = _environment.IsWindows
                    && (binaryPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                        || binaryPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase));
                var request = new ProcessRunRequest(
                    isCommandShim ? "cmd.exe" : binaryPath,
                    Path.GetDirectoryName(binaryPath) ?? ".",
                    isCommandShim
                        ? ["/d", "/s", "/c", binaryPath, config.VersionFlag]
                        : [config.VersionFlag],
                    string.IsNullOrEmpty(executablePath)
                        ? null
                        : new Dictionary<string, string> { ["PATH"] = executablePath },
                    TimeSpan.FromSeconds(3));
                var result = _processRunner.RunAsync(request).GetAwaiter().GetResult();
                if (!result.Started)
                {
                    _logger?.BinaryVersionProbeFailed(binaryPath, "process did not start");
                }
                else if (result.TimedOut)
                {
                    _logger?.BinaryVersionProbeFailed(binaryPath, "process timed out");
                }
                var combined = (result.Stdout + "\n" + result.Stderr).Trim();
                var regex = string.IsNullOrEmpty(config.VersionRegex)
                    ? DefaultVersionRegex
                    : new Regex(config.VersionRegex, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
                var match = regex.Match(combined);
                if (match.Success)
                    version = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.BinaryVersionProbeFailed(binaryPath, ex.Message);
            }
        }

        var state = string.IsNullOrEmpty(version)
            ? AdapterDetectionState.Broken
            : AdapterDetectionState.Detected;
        _logger?.BinaryVersionProbed(binaryPath, version ?? "", state.ToString());
        return new BinaryDiscoveryResult(state, binaryPath, version);
    }

    private string ExpandTilde(string path)
    {
        if (!path.StartsWith('~'))
            return path;
        return path.StartsWith("~/", StringComparison.Ordinal)
            ? Path.Combine(_environment.HomeDirectory, path[2..])
            : Path.Combine(_environment.HomeDirectory, path[1..]);
    }

    private static IReadOnlyList<string> ResolvePathDirs(string path)
        => path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

    private static bool IsPosixStylePath(string path)
        => path.StartsWith('/') || (path.Length > 2 && path[0] == '/' && path[2] == '/');

    public void CachePath(string path) => _cachedPath = path;
    public string? GetCachedPath() => _cachedPath;
}
