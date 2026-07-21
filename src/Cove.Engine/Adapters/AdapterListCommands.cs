using Cove.Adapters;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Adapters;

public static class AdapterListCommands
{
    [CoveCommand("cove://commands/adapter.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.ManifestStore is not { } manifestStore)
            return Task.FromResult(ctx.Fail("not_ready", "manifest store not available"));

        var items = new List<AdapterListItemDto>();
        foreach (var manifest in manifestStore.LoadAll())
        {
            string? status = null;
            string? version = null;
            string? binaryPath = null;
            if (ctx.Launcher is { } launcher)
            {
                var detection = launcher.DescribeAdapterBinary(manifest);
                status = DetectionStatus(detection.State);
                version = detection.Version;
                binaryPath = detection.BinaryPath;
            }
            var realPath = ResolveRealPath(binaryPath);
            items.Add(new AdapterListItemDto(manifest.Name, manifest.DisplayName, manifest.Accent, manifest.Binary, status, version, binaryPath, ResolveUpdateCommand(manifest, realPath), ResolveUninstallCommand(manifest, realPath), ResolveInstallCommand(manifest), manifest.Description, AdapterToolsCommands.LoadIcon(manifestStore, manifest)));
        }

        return Task.FromResult(ctx.Ok(new AdapterListResult(items), CoveJsonContext.Default.AdapterListResult));
    }

    public static string? ResolveUpdateCommand(AdapterManifest manifest, string? binaryRealPath)
    {
        if (PlatformRecipe(manifest.Update) is { } explicitUpdate)
            return explicitUpdate;

        var path = binaryRealPath ?? "";
        if (path.Contains("/Cellar/", StringComparison.Ordinal) || path.Contains("/Caskroom/", StringComparison.Ordinal))
            return $"brew upgrade {BrewName(manifest, path)}";

        var npmPackage = KnownNpmPackage(manifest.Name);
        if (npmPackage is not null && path.Contains("/.bun/", StringComparison.Ordinal))
            return $"bun install -g {npmPackage}@latest";
        if (npmPackage is not null && path.Contains("/node_modules/", StringComparison.Ordinal))
            return $"npm install -g --allow-scripts={npmPackage} {npmPackage}@latest";

        if (PlatformRecipe(manifest.Install) is { } installRecipe)
            return installRecipe;

        if (npmPackage is null)
            return null;
        return $"npm install -g --allow-scripts={npmPackage} {npmPackage}@latest";
    }

    public static string? ResolveInstallCommand(AdapterManifest manifest)
    {
        if (PlatformRecipe(manifest.Install) is { } recipe)
            return recipe;
        var npmPackage = KnownNpmPackage(manifest.Name);
        if (npmPackage is null)
            return null;
        return $"npm install -g --allow-scripts={npmPackage} {npmPackage}@latest";
    }

    public static string? ResolveUninstallCommand(AdapterManifest manifest, string? binaryRealPath)
    {
        if (PlatformRecipe(manifest.Uninstall) is { } explicitUninstall)
            return explicitUninstall;

        var path = binaryRealPath ?? "";
        if (path.Contains("/Cellar/", StringComparison.Ordinal) || path.Contains("/Caskroom/", StringComparison.Ordinal))
            return $"brew uninstall {BrewName(manifest, path)}";

        var npmPackage = KnownNpmPackage(manifest.Name);
        if (npmPackage is null)
            return null;
        if (path.Contains("/.bun/", StringComparison.Ordinal))
            return $"bun remove -g {npmPackage}";
        if (path.Contains("/node_modules/", StringComparison.Ordinal))
            return $"npm uninstall -g {npmPackage}";
        return null;
    }

    private static string? PlatformRecipe(PlatformRecipes? recipes)
    {
        var recipe = OperatingSystem.IsWindows()
            ? recipes?.Windows
            : OperatingSystem.IsMacOS()
                ? recipes?.Macos
                : recipes?.Linux;
        return !string.IsNullOrWhiteSpace(recipe?.Cmd) ? recipe.Cmd : null;
    }

    public static string? KnownNpmPackage(string adapterName) => adapterName switch
    {
        "claude-code" => "@anthropic-ai/claude-code",
        "codex" => "@openai/codex",
        "gemini" => "@google/gemini-cli",
        "omp" => "@oh-my-pi/pi-coding-agent",
        "opencode" => "opencode-ai",
        "pi" => "@earendil-works/pi-coding-agent",
        "openclaw" => "openclaw",
        _ => null,
    };

    private static string BrewName(AdapterManifest manifest, string path)
    {
        var formula = BrewPathSegment(path, "/Cellar/") ?? BrewPathSegment(path, "/Caskroom/");
        if (formula is not null)
            return formula;
        return manifest.Name switch
        {
            "gemini" => "gemini-cli",
            _ => manifest.Name,
        };
    }

    private static string? BrewPathSegment(string path, string marker)
    {
        var at = path.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0)
            return null;
        var start = at + marker.Length;
        var end = path.IndexOf('/', start);
        var segment = end < 0 ? path[start..] : path[start..end];
        return segment.Length > 0 ? segment : null;
    }

    public static string? ResolveRealPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return path;
        try
        {
            return System.IO.File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? path;
        }
        catch (System.IO.IOException)
        {
            return path;
        }
    }

    private static string DetectionStatus(AdapterDetectionState state) => state switch
    {
        AdapterDetectionState.Detected => "detected",
        AdapterDetectionState.Broken => "broken",
        _ => "missing",
    };
}
