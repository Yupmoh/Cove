using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Cove.Adapters;

public sealed record BinaryDiscoveryResult(
    AdapterDetectionState State,
    string? BinaryPath,
    string? Version);

public sealed class BinaryDiscoveryService
{
    private static readonly Regex VersionRegex = new(@"(\d+\.\d+\.\d+)", RegexOptions.Compiled);
    private string? _cachedPath;

    public BinaryDiscoveryResult Discover(BinaryDiscovery config, IReadOnlyList<string>? wellKnownPaths = null, string? loginShellPath = null)
    {
        var pathDirs = ResolvePathDirs(loginShellPath);

        foreach (var cmd in config.Commands)
        {
            foreach (var dir in pathDirs)
            {
                var candidate = Path.Combine(dir, cmd);
                if (File.Exists(candidate))
                    return ProbeVersion(candidate, config);
            }
        }

        if (wellKnownPaths is not null)
            foreach (var wk in wellKnownPaths)
            {
                var expanded = ExpandTilde(wk);
                foreach (var cmd in config.Commands)
                {
                    var candidate = Path.Combine(expanded, cmd);
                    if (File.Exists(candidate))
                        return ProbeVersion(candidate, config);
                }
            }

        return new BinaryDiscoveryResult(AdapterDetectionState.Missing, null, null);
    }

    private static BinaryDiscoveryResult ProbeVersion(string binaryPath, BinaryDiscovery config)
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
                        try { proc.Kill(true); } catch { }
                    }

                    var combined = (stdout + "\n" + stderr).Trim();
                    var match = VersionRegex.Match(combined);
                    if (match.Success)
                        version = match.Groups[1].Value;
                }
            }
            catch { }
        }

        var state = string.IsNullOrEmpty(version)
            ? AdapterDetectionState.Broken
            : AdapterDetectionState.Detected;
        return new BinaryDiscoveryResult(state, binaryPath, version);
    }

    private IReadOnlyList<string> ResolvePathDirs(string? loginShellPath)
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
