using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Cove.Adapters;

public sealed record BundledSeedReport(
    IReadOnlyList<string> Copied,
    IReadOnlyList<string> Refreshed,
    IReadOnlyList<string> SkippedUserManaged);

public static class BundledAdapterSeeder
{
    public const string StampFileName = ".bundled-stamp";
    private const string SentinelAdapter = "claude-code";

    public static string? ResolveSourceDir(string baseDirectory)
    {
        var current = new DirectoryInfo(baseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "adapters");
            if (File.Exists(Path.Combine(candidate, SentinelAdapter, "adapter.json")))
                return candidate;
            current = current.Parent;
        }
        return null;
    }

    public static string ComputeDirHash(string dir)
    {
        var files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Where(f => !string.Equals(Path.GetFileName(f), StampFileName, StringComparison.Ordinal))
            .Select(f => (Rel: RelativePosixPath(dir, f), Full: f))
            .OrderBy(x => x.Rel, StringComparer.Ordinal)
            .ToList();

        using var incremental = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var (rel, full) in files)
        {
            incremental.AppendData(Encoding.UTF8.GetBytes(rel));
            incremental.AppendData(new byte[] { 0 });
            var bytes = File.ReadAllBytes(full);
            incremental.AppendData(BitConverter.GetBytes((long)bytes.Length));
            incremental.AppendData(bytes);
        }
        return Convert.ToHexString(incremental.GetHashAndReset());
    }

    public static BundledSeedReport SeedFromBinaryLocation(string targetRoot, ILogger? logger = null)
    {
        var source = ResolveSourceDir(AppContext.BaseDirectory);
        if (source is null)
        {
            logger?.BundledAdapterSourceMissing(AppContext.BaseDirectory);
            return new BundledSeedReport(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        }
        return Seed(source, targetRoot, logger);
    }

    public static BundledSeedReport Seed(string sourceRoot, string targetRoot, ILogger? logger = null)
    {
        var copied = new List<string>();
        var refreshed = new List<string>();
        var skipped = new List<string>();

        if (!Directory.Exists(sourceRoot))
        {
            logger?.BundledAdapterSourceMissing(sourceRoot);
            return new BundledSeedReport(copied, refreshed, skipped);
        }

        Directory.CreateDirectory(targetRoot);

        foreach (var sourceDir in Directory.EnumerateDirectories(sourceRoot))
        {
            var name = Path.GetFileName(sourceDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(name))
                continue;
            if (!File.Exists(Path.Combine(sourceDir, "adapter.json")))
                continue;

            var targetDir = Path.Combine(targetRoot, name);
            var sourceHash = ComputeDirHash(sourceDir);

            if (!Directory.Exists(targetDir))
            {
                CopyRecursive(sourceDir, targetDir);
                WriteStamp(targetDir, sourceHash);
                copied.Add(name);
                logger?.BundledAdapterSeeded(name);
                continue;
            }

            var stampPath = Path.Combine(targetDir, StampFileName);
            if (!File.Exists(stampPath))
            {
                skipped.Add(name);
                logger?.BundledAdapterUserManaged(name);
                continue;
            }

            var existing = ReadStamp(stampPath);
            if (!string.Equals(existing, sourceHash, StringComparison.Ordinal))
            {
                Directory.Delete(targetDir, recursive: true);
                CopyRecursive(sourceDir, targetDir);
                WriteStamp(targetDir, sourceHash);
                refreshed.Add(name);
                logger?.BundledAdapterRefreshed(name);
            }
        }

        return new BundledSeedReport(copied, refreshed, skipped);
    }

    private static void WriteStamp(string adapterDir, string hash)
        => File.WriteAllText(Path.Combine(adapterDir, StampFileName), hash);

    private static string ReadStamp(string stampPath)
    {
        try { return File.ReadAllText(stampPath).Trim(); }
        catch (IOException) { return string.Empty; }
    }

    private static void CopyRecursive(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.EnumerateDirectories(source))
        {
            var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (name is ".git" or "node_modules")
                continue;
            CopyRecursive(dir, Path.Combine(dest, name));
        }
        foreach (var file in Directory.EnumerateFiles(source))
        {
            if (string.Equals(Path.GetFileName(file), StampFileName, StringComparison.Ordinal))
                continue;
            var destFile = Path.Combine(dest, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
            SetExecutableBitIfScript(destFile);
        }
    }

    private static void SetExecutableBitIfScript(string file)
    {
        if (OperatingSystem.IsWindows())
            return;
        if (!file.EndsWith(".sh", StringComparison.Ordinal))
            return;
        try
        {
            File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }
        catch (IOException) { }
    }

    private static string RelativePosixPath(string root, string full)
        => Path.GetRelativePath(root, full).Replace(Path.DirectorySeparatorChar, '/');
}
