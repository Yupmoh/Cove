using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cove.Platform;
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

    public static BundledSeedReport SeedFromBinaryLocation(
        string targetRoot,
        ILogger? logger = null,
        IExecutableMode? executableMode = null)
    {
        var source = ResolveSourceDir(AppContext.BaseDirectory);
        if (source is null)
        {
            logger?.BundledAdapterSourceMissing(AppContext.BaseDirectory);
            return new BundledSeedReport(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        }
        var report = Seed(source, targetRoot, logger, executableMode);
        InstallSkills(
            source,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            logger);
        return report;
    }

    public static BundledSeedReport Seed(
        string sourceRoot,
        string targetRoot,
        ILogger? logger = null,
        IExecutableMode? executableMode = null)
    {
        executableMode ??= new SystemExecutableMode();
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
                CopyRecursive(sourceDir, targetDir, logger, executableMode);
                WriteStamp(targetDir, sourceHash);
                copied.Add(name);
                logger?.BundledAdapterSeeded(name);
                continue;
            }

            var stampPath = Path.Combine(targetDir, StampFileName);
            if (!File.Exists(stampPath))
            {
                if (IsLegacyBundledAdapter(targetDir, name))
                {
                    Directory.Delete(targetDir, recursive: true);
                    CopyRecursive(sourceDir, targetDir, logger, executableMode);
                    WriteStamp(targetDir, sourceHash);
                    refreshed.Add(name);
                    logger?.BundledAdapterRefreshed(name);
                }
                else
                {
                    skipped.Add(name);
                    logger?.BundledAdapterUserManaged(name);
                }
                continue;
            }

            var existing = ReadStamp(stampPath, logger);
            if (!string.Equals(existing, sourceHash, StringComparison.Ordinal))
            {
                Directory.Delete(targetDir, recursive: true);
                CopyRecursive(sourceDir, targetDir, logger, executableMode);
                WriteStamp(targetDir, sourceHash);
                refreshed.Add(name);
                logger?.BundledAdapterRefreshed(name);
            }
        }

        var canonicalSkill = Path.Combine(sourceRoot, "cove", "skill.md");
        if (File.Exists(canonicalSkill))
        {
            var canonicalTarget = Path.Combine(targetRoot, "cove", "skill.md");
            Directory.CreateDirectory(Path.GetDirectoryName(canonicalTarget)!);
            File.Copy(canonicalSkill, canonicalTarget, overwrite: true);
        }

        return new BundledSeedReport(copied, refreshed, skipped);
    }

    public static IReadOnlyList<string> InstallSkills(
        string sourceRoot,
        string homeDirectory,
        ILogger? logger = null)
    {
        var installed = new List<string>();
        var installedDestinations = new HashSet<string>(
            StringComparer.Ordinal);
        if (!Directory.Exists(sourceRoot))
        {
            logger?.BundledAdapterSourceMissing(sourceRoot);
            return installed;
        }

        foreach (var sourceDir in Directory
                     .EnumerateDirectories(sourceRoot)
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(sourceDir.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
            var manifestPath = Path.Combine(sourceDir, "adapter.json");
            if (string.IsNullOrEmpty(name) || !File.Exists(manifestPath))
                continue;
            try
            {
                using var document = JsonDocument.Parse(
                    File.ReadAllText(manifestPath));
                if (!document.RootElement.TryGetProperty(
                        "skillInstallPath",
                        out var installPathElement))
                    continue;
                var installPath = installPathElement.GetString();
                if (string.IsNullOrWhiteSpace(installPath))
                {
                    logger?.SkillInstallFailed(
                        name,
                        installPath ?? string.Empty,
                        "skill install path is empty");
                    continue;
                }
                var adapterSkill = Path.Combine(sourceDir, "skill.md");
                var sharedSkill = Path.Combine(
                    sourceRoot,
                    "cove",
                    "skill.md");
                var sourceSkill = File.Exists(adapterSkill)
                    ? adapterSkill
                    : sharedSkill;
                if (!File.Exists(sourceSkill))
                {
                    logger?.SkillInstallSkipped(name, installPath);
                    continue;
                }
                var destination = ExpandHome(installPath, homeDirectory);
                var destinationDirectory = Path.GetDirectoryName(destination);
                if (destinationDirectory is null)
                {
                    logger?.SkillInstallFailed(
                        name,
                        installPath,
                        "skill install path has no directory");
                    continue;
                }
                Directory.CreateDirectory(destinationDirectory);
                if (installedDestinations.Add(destination))
                    File.Copy(sourceSkill, destination, overwrite: true);
                installed.Add(name);
            }
            catch (IOException ex)
            {
                logger?.SkillInstallFailed(name, manifestPath, ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger?.SkillInstallFailed(name, manifestPath, ex.Message);
            }
            catch (JsonException ex)
            {
                logger?.SkillInstallFailed(name, manifestPath, ex.Message);
            }
        }
        return installed;
    }

    private static bool IsLegacyBundledAdapter(string adapterDir, string name)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(adapterDir, "adapter.json")));
            var root = document.RootElement;
            return root.TryGetProperty("name", out var manifestName)
                && string.Equals(manifestName.GetString(), name, StringComparison.Ordinal)
                && root.TryGetProperty("author", out var author)
                && string.Equals(author.GetString(), "Cove", StringComparison.Ordinal);
        }
        catch (IOException) { return false; }
        catch (JsonException) { return false; }
    }

    private static void WriteStamp(string adapterDir, string hash)
        => File.WriteAllText(Path.Combine(adapterDir, StampFileName), hash);

    private static string ReadStamp(string stampPath, ILogger? logger)
    {
        try { return File.ReadAllText(stampPath).Trim(); }
        catch (IOException ex) { logger?.BundledStampReadFailed(stampPath, ex.Message); return string.Empty; }
    }

    private static void CopyRecursive(string source, string dest, ILogger? logger, IExecutableMode executableMode)
    {
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.EnumerateDirectories(source))
        {
            var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (name is ".git" or "node_modules")
                continue;
            CopyRecursive(dir, Path.Combine(dest, name), logger, executableMode);
        }
        foreach (var file in Directory.EnumerateFiles(source))
        {
            if (string.Equals(Path.GetFileName(file), StampFileName, StringComparison.Ordinal))
                continue;
            var destFile = Path.Combine(dest, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
            SetExecutableBitIfScript(destFile, logger, executableMode);
        }
    }

    private static void SetExecutableBitIfScript(string file, ILogger? logger, IExecutableMode executableMode)
    {
        if (!file.EndsWith(".sh", StringComparison.Ordinal))
            return;
        try
        {
            executableMode.MakeUserExecutable(file);
        }
        catch (IOException ex) { logger?.BundledSetExecutableFailed(file, ex.Message); }
    }

    private static string RelativePosixPath(string root, string full)
        => Path.GetRelativePath(root, full).Replace(Path.DirectorySeparatorChar, '/');

    private static string ExpandHome(
        string path,
        string homeDirectory)
    {
        if (path == "~")
            return homeDirectory;
        if (path.StartsWith("~/", StringComparison.Ordinal)
            || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            return Path.Combine(
                homeDirectory,
                path[2..].Replace(
                    '/',
                    Path.DirectorySeparatorChar));
        }
        return path;
    }
}
