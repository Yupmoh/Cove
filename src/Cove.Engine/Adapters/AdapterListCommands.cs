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
            items.Add(new AdapterListItemDto(manifest.Name, manifest.DisplayName, manifest.Accent, manifest.Binary, status, version, binaryPath, ResolveUpdateCommand(manifest)));
        }

        return Task.FromResult(ctx.Ok(new AdapterListResult(items), CoveJsonContext.Default.AdapterListResult));
    }

    public static string? ResolveUpdateCommand(AdapterManifest manifest)
    {
        var platform = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux";
        if (manifest.Install.TryGetValue(platform, out var recipe) && !string.IsNullOrWhiteSpace(recipe.Cmd))
            return recipe.Cmd;
        return manifest.Name switch
        {
            "claude-code" => "npm install -g @anthropic-ai/claude-code@latest",
            "codex" => "npm install -g @openai/codex@latest",
            "gemini" => "npm install -g @google/gemini-cli@latest",
            "omp" => "bun install -g @oh-my-pi/pi-coding-agent@latest",
            _ => null,
        };
    }

    private static string DetectionStatus(AdapterDetectionState state) => state switch
    {
        AdapterDetectionState.Detected => "detected",
        AdapterDetectionState.Broken => "broken",
        _ => "missing",
    };
}
