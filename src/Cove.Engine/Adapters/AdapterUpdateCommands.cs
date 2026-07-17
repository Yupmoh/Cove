using Cove.Adapters;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Adapters;

public static class AdapterUpdateCommands
{
    private static HarnessUpdateChecker? _checker;

    public static void Configure(HarnessUpdateChecker checker) => _checker = checker;

    [CoveCommand("cove://commands/adapter.updates-check")]
    public static async Task<ControlResponse> UpdatesCheck(EngineDispatchContext ctx)
    {
        if (ctx.ManifestStore is not { } manifestStore)
            return ctx.Fail("not_ready", "manifest store not available");
        if (ctx.Launcher is not { } launcher)
            return ctx.Fail("not_ready", "launcher not available");
        var checker = _checker ??= HarnessUpdateChecker.CreateNpm();

        var candidates = new List<(AdapterManifest Manifest, string Installed, string Package, string? BinaryPath)>();
        foreach (var manifest in manifestStore.LoadAll())
        {
            if (AdapterListCommands.KnownNpmPackage(manifest.Name) is not { } package)
                continue;
            var detection = launcher.DescribeAdapterBinary(manifest);
            if (detection.State != AdapterDetectionState.Detected || string.IsNullOrEmpty(detection.Version))
                continue;
            candidates.Add((manifest, detection.Version, package, detection.BinaryPath));
        }

        var latests = await Task.WhenAll(candidates.Select(c => checker.GetLatestVersionAsync(c.Package))).ConfigureAwait(false);

        var updates = new List<HarnessUpdateDto>();
        for (var i = 0; i < candidates.Count; i++)
        {
            var (manifest, installed, _, binaryPath) = candidates[i];
            if (latests[i] is not { } latest || !HarnessUpdateChecker.IsNewer(latest, installed))
                continue;
            var realPath = AdapterListCommands.ResolveRealPath(binaryPath);
            updates.Add(new HarnessUpdateDto(manifest.Name, manifest.DisplayName, installed, latest, AdapterListCommands.ResolveUpdateCommand(manifest, realPath)));
        }

        return ctx.Ok(new HarnessUpdatesResult(updates), CoveJsonContext.Default.HarnessUpdatesResult);
    }
}
