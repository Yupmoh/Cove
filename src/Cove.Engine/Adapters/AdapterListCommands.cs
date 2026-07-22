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
        var lifecycleResolver = new AdapterLifecycleCommandResolver();
        foreach (var manifest in manifestStore.LoadAll())
        {
            string? status = null;
            string? version = null;
            string? binaryPath = null;
            var detectionState = AdapterDetectionState.Missing;
            if (ctx.Launcher is { } launcher)
            {
                var detection = launcher.DescribeAdapterBinary(manifest);
                detectionState = detection.State;
                status = DetectionStatus(detection.State);
                version = detection.Version;
                binaryPath = detection.BinaryPath;
            }
            var realPath = ResolveRealPath(binaryPath);
            var lifecycle = lifecycleResolver.Resolve(
                manifest,
                detectionState,
                binaryPath,
                realPath);
            items.Add(new AdapterListItemDto(
                manifest.Name,
                manifest.DisplayName,
                manifest.Accent,
                manifest.Binary,
                status,
                version,
                binaryPath,
                lifecycle.UpdateCommand,
                lifecycle.UninstallCommand,
                lifecycle.InstallCommand,
                manifest.Description,
                AdapterToolsCommands.LoadIcon(manifestStore, manifest),
                lifecycle.Provenance));
        }

        return Task.FromResult(ctx.Ok(new AdapterListResult(items), CoveJsonContext.Default.AdapterListResult));
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
