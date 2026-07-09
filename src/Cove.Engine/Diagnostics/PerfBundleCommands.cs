using System.Linq;
using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Diagnostics;

public static class PerfBundleCommands
{
    [CoveCommand("cove://commands/perf.bundle.create")]
    public static Task<ControlResponse> Create(EngineDispatchContext ctx)
    {
        if (ctx.PerfBundles is not { } svc)
            return Task.FromResult(ctx.Fail("not_ready", "performance bundle service not available"));

        var tracePath = ctx.Request.Params is JsonElement el && el.Deserialize(CoveJsonContext.Default.PerfBundleCreateParams) is { } p
            ? p.TracePath
            : null;
        if (string.IsNullOrEmpty(tracePath))
            tracePath = null;

        var bundle = svc.CreateBundle(tracePath);
        return Task.FromResult(ctx.Ok(ToDto(bundle), CoveJsonContext.Default.PerfBundleDto));
    }

    [CoveCommand("cove://commands/perf.bundle.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.PerfBundles is not { } svc)
            return Task.FromResult(ctx.Fail("not_ready", "performance bundle service not available"));

        var bundles = svc.ListBundles().Select(ToDto).ToList();
        return Task.FromResult(ctx.Ok(new PerfBundleListResult(bundles), CoveJsonContext.Default.PerfBundleListResult));
    }

    [CoveCommand("cove://commands/perf.bundle.delete")]
    public static Task<ControlResponse> Delete(EngineDispatchContext ctx)
    {
        if (ctx.PerfBundles is not { } svc)
            return Task.FromResult(ctx.Fail("not_ready", "performance bundle service not available"));

        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(CoveJsonContext.Default.PerfBundleDeleteParams) is not { } p
            || string.IsNullOrEmpty(p.BundlePath))
            return Task.FromResult(ctx.Fail("invalid_params", "bundlePath required"));

        var deleted = svc.DeleteBundle(p.BundlePath);
        return Task.FromResult(deleted ? ctx.Ok() : ctx.Fail("not_found", "performance bundle not found"));
    }

    private static PerfBundleDto ToDto(PerformanceBundle bundle) =>
        new(bundle.Id, bundle.BundlePath, bundle.CreatedAt.ToString("o"), bundle.SizeBytes, bundle.SnapshotCount, bundle.ContainsTrace);
}
