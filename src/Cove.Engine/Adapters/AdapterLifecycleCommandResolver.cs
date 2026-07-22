using System.Text.Json;
using Cove.Adapters;
using Cove.Platform;

namespace Cove.Engine.Adapters;

public sealed record AdapterLifecycleCommandResult(
    string Provenance,
    string? InstallCommand,
    string? UpdateCommand,
    string? UninstallCommand);

public sealed class AdapterLifecycleCommandResolver
{
    private readonly IRuntimeEnvironment _environment;
    private readonly IPlatformFileSystem _fileSystem;

    public AdapterLifecycleCommandResolver(
        IRuntimeEnvironment? environment = null,
        IPlatformFileSystem? fileSystem = null)
    {
        _environment = environment ?? SystemRuntimeEnvironment.Instance;
        _fileSystem = fileSystem ?? SystemPlatformFileSystem.Instance;
    }

    public AdapterLifecycleCommandResult Resolve(
        AdapterManifest manifest,
        AdapterDetectionState detectionState,
        string? binaryPath,
        string? resolvedTargetPath)
    {
        var provenance = ResolveProvenance(
            manifest,
            detectionState,
            binaryPath,
            resolvedTargetPath,
            out var packageName);
        var installCommand = PlatformRecipe(manifest.Install)
            ?? NpmInstall(manifest.PackageIdentity?.Npm);
        var updateCommand = PlatformRecipe(manifest.Update)
            ?? GeneratedUpdate(provenance, packageName);
        var uninstallCommand = PlatformRecipe(manifest.Uninstall)
            ?? GeneratedUninstall(provenance, packageName);
        return new AdapterLifecycleCommandResult(
            provenance,
            installCommand,
            updateCommand,
            uninstallCommand);
    }

    private string ResolveProvenance(
        AdapterManifest manifest,
        AdapterDetectionState detectionState,
        string? binaryPath,
        string? resolvedTargetPath,
        out string? packageName)
    {
        packageName = null;
        if (detectionState != AdapterDetectionState.Detected
            || string.IsNullOrWhiteSpace(binaryPath))
        {
            return "unknown";
        }

        var binary = Normalize(binaryPath);
        var target = Normalize(resolvedTargetPath ?? binaryPath);
        var brewSegment = BrewPackageSegment(target, out var brewMarkerFound);
        if (brewMarkerFound)
        {
            packageName = brewSegment ?? manifest.PackageIdentity?.Brew;
            if (!string.IsNullOrWhiteSpace(packageName))
                return "brew";
        }

        var npmPackage = manifest.PackageIdentity?.Npm;
        if (!string.IsNullOrWhiteSpace(npmPackage))
        {
            var bunRoot = Normalize(Path.Combine(
                _environment.HomeDirectory,
                ".bun",
                "install",
                "global",
                "node_modules"));
            if ((IsUnder(binary, bunRoot) || IsUnder(target, bunRoot))
                && IsPackagePath(target, bunRoot, npmPackage))
            {
                packageName = npmPackage;
                return "bun";
            }

            if (_environment.IsWindows
                && IsWindowsNpmShim(binaryPath, npmPackage))
            {
                packageName = npmPackage;
                return "npm";
            }

            if (!_environment.IsWindows && IsUnixGlobalNpmTarget(target, npmPackage))
            {
                packageName = npmPackage;
                return "npm";
            }
        }

        if (_environment.IsWindows
            && binary.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            return "unknown";
        }
        return "native";
    }

    private bool IsWindowsNpmShim(string binaryPath, string packageName)
    {
        var appData = _environment.GetEnvironmentVariable("APPDATA");
        if (string.IsNullOrWhiteSpace(appData)
            || !binaryPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var npmRoot = Path.Combine(appData, "npm");
        var normalizedRoot = Normalize(npmRoot);
        var normalizedBinary = Normalize(binaryPath);
        var slash = normalizedBinary.LastIndexOf('/');
        var binaryDirectory = slash < 0 ? "" : normalizedBinary[..slash];
        if (!string.Equals(
                binaryDirectory,
                normalizedRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var packagePath = Path.Combine(
            [npmRoot, "node_modules", .. packageName.Split('/')]);
        var packageJsonPath = Path.Combine(packagePath, "package.json");
        if (!_fileSystem.FileExists(packageJsonPath))
            return false;

        try
        {
            using var document = JsonDocument.Parse(_fileSystem.ReadAllText(packageJsonPath));
            return document.RootElement.TryGetProperty("name", out var name)
                && string.Equals(
                    name.GetString(),
                    packageName,
                    StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsUnixGlobalNpmTarget(string path, string packageName)
    {
        var marker = "/lib/node_modules";
        var at = path.IndexOf(marker + "/", StringComparison.Ordinal);
        return at >= 0
            && IsPackagePath(path, path[..(at + marker.Length)], packageName);
    }

    private static bool IsPackagePath(
        string path,
        string root,
        string packageName)
    {
        var packageRoot = root.TrimEnd('/') + "/" + packageName;
        return string.Equals(path, packageRoot, StringComparison.Ordinal)
            || path.StartsWith(packageRoot + "/", StringComparison.Ordinal);
    }

    private static bool IsUnder(string path, string root)
        => string.Equals(path, root, StringComparison.Ordinal)
            || path.StartsWith(root.TrimEnd('/') + "/", StringComparison.Ordinal);

    private static string? BrewPackageSegment(
        string path,
        out bool markerFound)
    {
        foreach (var marker in new[] { "/Cellar", "/Caskroom" })
        {
            var at = path.IndexOf(marker, StringComparison.Ordinal);
            if (at < 0)
                continue;
            var afterMarker = at + marker.Length;
            if (afterMarker < path.Length && path[afterMarker] != '/')
                continue;
            markerFound = true;
            if (afterMarker == path.Length)
                return null;
            var start = afterMarker + 1;
            var end = path.IndexOf('/', start);
            var segment = end < 0 ? path[start..] : path[start..end];
            return segment.Length == 0 ? null : segment;
        }
        markerFound = false;
        return null;
    }

    private string? PlatformRecipe(PlatformRecipes? recipes)
    {
        var recipe = _environment.IsWindows
            ? recipes?.Windows
            : _environment.IsMacOS
                ? recipes?.Macos
                : recipes?.Linux;
        return !string.IsNullOrWhiteSpace(recipe?.Cmd) ? recipe.Cmd : null;
    }

    private static string? GeneratedUpdate(
        string provenance,
        string? packageName)
        => provenance switch
        {
            "npm" when packageName is not null => NpmInstall(packageName),
            "bun" when packageName is not null => $"bun install -g {packageName}@latest",
            "brew" when packageName is not null => $"brew upgrade {packageName}",
            _ => null,
        };

    private static string? GeneratedUninstall(
        string provenance,
        string? packageName)
        => provenance switch
        {
            "npm" when packageName is not null => $"npm uninstall -g {packageName}",
            "bun" when packageName is not null => $"bun remove -g {packageName}",
            "brew" when packageName is not null => $"brew uninstall {packageName}",
            _ => null,
        };

    private static string? NpmInstall(string? packageName)
        => string.IsNullOrWhiteSpace(packageName)
            ? null
            : $"npm install -g --allow-scripts={packageName} {packageName}@latest";

    private static string Normalize(string path)
        => path.Replace('\\', '/').TrimEnd('/');
}
