using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Cove.Adapters;

public sealed record BinaryDiscoveryResult(
    AdapterDetectionState State,
    string? BinaryPath,
    string? Version);

public sealed class BinaryDiscoveryService
{
    private static readonly Regex VersionRegex = new(@"(\d+\.\d+\.\d+)", RegexOptions.Compiled);
    private readonly ILogger? _logger;
    private string? _cachedPath;

    public BinaryDiscoveryService(ILogger? logger = null)
    {
        _logger = logger;
    }

    public BinaryDiscoveryResult Discover(BinaryDiscovery config, IReadOnlyList<string>? wellKnownPaths = null, string? loginShellPath = null)
    {
        var source = loginShellPath is not null ? "login-shell" : "process-env";
        var rawPath = loginShellPath ?? Environment.GetEnvironmentVariable("PATH") ?? "";
        var pathDirs = ResolvePathDirs(loginShellPath);
        var commands = string.Join(",", config.Commands);
        var wellKnownCount = wellKnownPaths?.Count ?? 0;

        _logger?.BinaryProbeStarted(commands, source, pathDirs.Count, wellKnownCount);
        _logger?.BinaryProbePathContents(source, rawPath);

        foreach (var cmd in config.Commands)
        {
            foreach (var dir in pathDirs)
            {
                var candidate = Path.Combine(dir, cmd);
                var exists = File.Exists(candidate);
                _logger?.BinaryCandidateTested(cmd, candidate, exists, "path");
                if (exists)
                    return Resolve(cmd, candidate, config, "path");
            }
        }

        if (wellKnownPaths is not null)
            foreach (var wk in wellKnownPaths)
            {
                var expanded = ExpandTilde(wk);
                foreach (var cmd in config.Commands)
                {
                    var candidate = Path.Combine(expanded, cmd);
                    var exists = File.Exists(candidate);
                    _logger?.BinaryCandidateTested(cmd, candidate, exists, "well-known");
                    if (exists)
                        return Resolve(cmd, candidate, config, "well-known");
                }
            }

        _logger?.BinaryProbeMissing(commands, pathDirs.Count, wellKnownCount);
        return new BinaryDiscoveryResult(AdapterDetectionState.Missing, null, null);
    }

    private BinaryDiscoveryResult Resolve(string command, string candidate, BinaryDiscovery config, string probe)
    {
        _logger?.BinaryResolved(command, candidate, probe);
        if (OperatingSystem.IsWindows() && IsPosixStylePath(candidate))
            _logger?.BinaryResolvedNonRunnableWindowsPath(command, candidate);
        return ProbeVersion(candidate, config);
    }

    private static bool IsPosixStylePath(string path)
        => path.StartsWith('/') || (path.Length > 2 && path[0] == '/' && path[2] == '/');

    private BinaryDiscoveryResult ProbeVersion(string binaryPath, BinaryDiscovery config)
    {
        string? version = null;
        if (!string.IsNullOrEmpty(config.VersionFlag))
        {
            try
            {
                var psi = new ProcessStartInfo(binaryPath)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                psi.ArgumentList.Add(config.VersionFlag);

                using var proc = Process.Start(psi);
                if (proc is not null)
                {
                    var stdout = proc.StandardOutput.ReadToEnd();
                    var stderr = proc.StandardError.ReadToEnd();
                    if (!proc.WaitForExit(3000))
                    {
                        try { proc.Kill(true); }
                        catch (Exception ex) { _logger?.BinaryVersionProbeKillFailed(binaryPath, ex.Message); }
                    }

                    var combined = (stdout + "\n" + stderr).Trim();
                    var match = VersionRegex.Match(combined);
                    if (match.Success)
                        version = match.Groups[1].Value;
                }
            }
            catch (Exception ex)
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

    private static IReadOnlyList<string> ResolvePathDirs(string? loginShellPath)
    {
        var path = loginShellPath ?? Environment.GetEnvironmentVariable("PATH") ?? "";
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
    }

    private static string ExpandTilde(string path)
    {
        if (path.StartsWith("~", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return path.StartsWith("~/", StringComparison.Ordinal)
                ? Path.Combine(home, path[2..])
                : Path.Combine(home, path[1..]);
        }
        return path;
    }

    public void CachePath(string path) => _cachedPath = path;
    public string? GetCachedPath() => _cachedPath;
}
