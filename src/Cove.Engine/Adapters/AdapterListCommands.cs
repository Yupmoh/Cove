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
            items.Add(new AdapterListItemDto(manifest.Name, manifest.DisplayName, manifest.Accent, manifest.Binary, status, version, binaryPath));
        }

        return Task.FromResult(ctx.Ok(new AdapterListResult(items), CoveJsonContext.Default.AdapterListResult));
    }

    private static string DetectionStatus(AdapterDetectionState state) => state switch
    {
        AdapterDetectionState.Detected => "detected",
        AdapterDetectionState.Broken => "broken",
        _ => "missing",
    };
}
