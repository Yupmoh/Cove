using System.Text.Json;
using Cove.Adapters;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Adapters;

public static class AdapterToolsCommands
{
    private static readonly HashSet<string> BundledAdapters = new(StringComparer.Ordinal)
    {
        "claude-code", "codex", "omp", "gemini",
    };

    [CoveCommand("cove://commands/adapter.tools-list")]
    public static Task<ControlResponse> ToolsList(EngineDispatchContext ctx)
        => Task.FromResult(BuildToolsList(ctx));

    [CoveCommand("cove://commands/adapter.rescan")]
    public static Task<ControlResponse> Rescan(EngineDispatchContext ctx)
    {
        ctx.Launcher?.RefreshLoginShellPath();
        return Task.FromResult(BuildToolsList(ctx));
    }

    [CoveCommand("cove://commands/adapter.install-local")]
    public static async Task<ControlResponse> InstallLocal(EngineDispatchContext ctx)
    {
        if (ctx.ManifestStore is not { } store)
            return ctx.Fail("not_ready", "manifest store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.AdapterInstallLocalParams) is not { } p || string.IsNullOrWhiteSpace(p.Path))
            return ctx.Fail("invalid_params", "a local folder path is required");

        var source = p.Path.Trim();
        var manifestPath = System.IO.Path.Combine(source, "adapter.json");
        if (!System.IO.File.Exists(manifestPath))
            return ctx.Fail("invalid_adapter", "no adapter.json found in the selected folder");

        AdapterManifest manifest;
        try
        {
            var json = await System.IO.File.ReadAllTextAsync(manifestPath).ConfigureAwait(false);
            var parsed = JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.AdapterManifest);
            if (parsed is null)
                return ctx.Fail("invalid_adapter", "adapter.json could not be parsed");
            manifest = parsed;
        }
        catch (JsonException ex)
        {
            return ctx.Fail("invalid_adapter", $"adapter.json is invalid: {ex.Message}");
        }

        var errors = ManifestValidator.Validate(manifest);
        if (errors.Count > 0)
            return ctx.Fail("invalid_adapter", $"{errors[0].Field}: {errors[0].Message}");

        var installer = new AdapterInstallService(new MethodRunner());
        try
        {
            var installed = await installer.InstallAsync(store.AdaptersRoot, manifest.Name, new LocalDirAdapterFetcher(source)).ConfigureAwait(false);
            store.Invalidate(installed.Name);
            return ctx.Ok(new AdapterInstallLocalResult(installed.Name), CoveJsonContext.Default.AdapterInstallLocalResult);
        }
        catch (AdapterInstallException ex)
        {
            return ctx.Fail("install_failed", ex.Message);
        }
    }

    [CoveCommand("cove://commands/adapter.remove")]
    public static async Task<ControlResponse> Remove(EngineDispatchContext ctx)
    {
        if (ctx.ManifestStore is not { } store)
            return ctx.Fail("not_ready", "manifest store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.AdapterRemoveParams) is not { } p || string.IsNullOrWhiteSpace(p.Name))
            return ctx.Fail("invalid_params", "adapter name is required");

        var name = p.Name.Trim();
        if (BundledAdapters.Contains(name))
            return ctx.Fail("not_removable", $"'{name}' is a bundled adapter and cannot be removed");

        var manifest = store.Load(name);
        var skillPath = ExpandTilde(manifest?.SkillInstallPath);
        var installer = new AdapterInstallService(new MethodRunner());
        await installer.UninstallAsync(store.AdaptersRoot, name, manifest, skillPath).ConfigureAwait(false);
        store.Invalidate(name);

        var purged = 0;
        if (p.PurgeSessions && ctx.RecentSessions is { } sessions)
            purged = sessions.PurgeAdapter(name);

        return ctx.Ok(new AdapterRemoveResult(name, purged), CoveJsonContext.Default.AdapterRemoveResult);
    }

    [CoveCommand("cove://commands/adapter.retention-get")]
    public static async Task<ControlResponse> RetentionGet(EngineDispatchContext ctx)
    {
        if (ctx.ManifestStore is not { } store)
            return ctx.Fail("not_ready", "manifest store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.AdapterNameParams) is not { } p || string.IsNullOrWhiteSpace(p.Name))
            return ctx.Fail("invalid_params", "adapter name is required");

        var manifest = store.Load(p.Name.Trim());
        if (manifest is null)
            return ctx.Fail("not_found", $"unknown adapter: {p.Name}");

        var dto = await ReadRetentionAsync(store, manifest).ConfigureAwait(false);
        return ctx.Ok(dto, CoveJsonContext.Default.ToolsRetentionDto);
    }

    [CoveCommand("cove://commands/adapter.retention-set")]
    public static async Task<ControlResponse> RetentionSet(EngineDispatchContext ctx)
    {
        if (ctx.ManifestStore is not { } store)
            return ctx.Fail("not_ready", "manifest store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.AdapterRetentionSetParams) is not { } p || string.IsNullOrWhiteSpace(p.Name))
            return ctx.Fail("invalid_params", "adapter name and value are required");

        var manifest = store.Load(p.Name.Trim());
        if (manifest is null)
            return ctx.Fail("not_found", $"unknown adapter: {p.Name}");
        if (manifest.Retention?.WriteScript is not { } writeScript)
            return ctx.Fail("unsupported", $"adapter '{p.Name}' does not support editing retention");

        var adapterDir = store.ResolveDir(manifest.Name);
        var runner = new MethodRunner();
        var result = await runner.RunAsync(adapterDir, writeScript, new[] { p.Value ?? "" }, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        if (result.Error)
            return ctx.Fail("write_failed", string.IsNullOrEmpty(result.Stderr) ? "retention write failed" : result.Stderr.Trim());

        var dto = await ReadRetentionAsync(store, manifest).ConfigureAwait(false);
        return ctx.Ok(dto, CoveJsonContext.Default.ToolsRetentionDto);
    }

    private static ControlResponse BuildToolsList(EngineDispatchContext ctx)
    {
        if (ctx.ManifestStore is not { } store)
            return ctx.Fail("not_ready", "manifest store not available");

        var items = new List<ToolsAdapterDto>();
        foreach (var manifest in store.LoadAll())
        {
            string? status = null, version = null, binaryPath = null;
            if (ctx.Launcher is { } launcher)
            {
                var detection = launcher.DescribeAdapterBinary(manifest);
                status = DetectionStatus(detection.State);
                version = detection.Version;
                binaryPath = detection.BinaryPath;
            }

            var bundled = BundledAdapters.Contains(manifest.Name);
            items.Add(new ToolsAdapterDto(
                manifest.Name,
                manifest.DisplayName,
                manifest.Accent,
                manifest.Binary,
                status,
                version,
                binaryPath,
                LoadIcon(store, manifest),
                InstallHint(manifest),
                bundled,
                !bundled,
                RetentionShape(manifest)));
        }

        return ctx.Ok(new ToolsListResult(items), CoveJsonContext.Default.ToolsListResult);
    }

    private static async Task<ToolsRetentionDto> ReadRetentionAsync(AdapterManifestStore store, AdapterManifest manifest)
    {
        if (manifest.Retention is not { } retention || string.IsNullOrEmpty(retention.ReadScript))
            return RetentionShape(manifest);

        var adapterDir = store.ResolveDir(manifest.Name);
        var runner = new MethodRunner();
        var result = await runner.RunAsync(adapterDir, retention.ReadScript, Array.Empty<string>(), TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        string? value = null;
        if (result.Ok)
            value = ExtractRetentionValue(result);

        var editable = !string.IsNullOrEmpty(retention.WriteScript);
        var hidden = RetentionThreshold.IsHidden(value, retention.Recommended);
        return new ToolsRetentionDto(true, editable, hidden, value, retention.Recommended);
    }

    private static string? ExtractRetentionValue(MethodResult result)
    {
        if (result.Json is { } json)
        {
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("value", out var v))
                return v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText();
            if (json.ValueKind is JsonValueKind.Number or JsonValueKind.String)
                return json.ValueKind == JsonValueKind.String ? json.GetString() : json.GetRawText();
        }
        var trimmed = result.Stdout.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static ToolsRetentionDto RetentionShape(AdapterManifest manifest)
    {
        if (manifest.Retention is not { } retention || string.IsNullOrEmpty(retention.ReadScript))
            return new ToolsRetentionDto(false, false, true, null, null);
        return new ToolsRetentionDto(true, !string.IsNullOrEmpty(retention.WriteScript), false, null, retention.Recommended);
    }

    private static string? LoadIcon(AdapterManifestStore store, AdapterManifest manifest)
    {
        if (string.IsNullOrEmpty(manifest.Icon))
            return null;
        var iconPath = System.IO.Path.Combine(store.ResolveDir(manifest.Name), manifest.Icon);
        if (!System.IO.File.Exists(iconPath))
            return null;
        try
        {
            return AdapterIconSanitizer.Sanitize(System.IO.File.ReadAllText(iconPath));
        }
        catch (System.IO.IOException)
        {
            return null;
        }
    }

    private static string InstallHint(AdapterManifest manifest)
    {
        var platform = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux";
        if (manifest.Install.TryGetValue(platform, out var recipe) && !string.IsNullOrWhiteSpace(recipe.Cmd))
            return recipe.Cmd;
        return manifest.Name switch
        {
            "claude-code" => "npm install -g @anthropic-ai/claude-code",
            "codex" => "npm install -g @openai/codex",
            "gemini" => "npm install -g @google/gemini-cli",
            "omp" => "Install omp from github.com/omp-cli/omp, then re-scan.",
            _ => $"Install the '{manifest.Binary}' CLI on your PATH, then re-scan.",
        };
    }

    private static string DetectionStatus(AdapterDetectionState state) => state switch
    {
        AdapterDetectionState.Detected => "detected",
        AdapterDetectionState.Broken => "broken",
        _ => "missing",
    };

    private static string? ExpandTilde(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return path;
        if (path == "~")
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (path.StartsWith("~/", StringComparison.Ordinal))
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
        return path;
    }
}
