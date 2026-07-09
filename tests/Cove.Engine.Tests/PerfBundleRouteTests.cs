using System.Text.Json;
using Cove.Engine.Diagnostics;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class PerfBundleRouteTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-perfroute-{System.Guid.NewGuid():N}");

    private static PerformanceBundleService NewService()
    {
        var hub = new DiagnosticsHub(new DiagnosticsConfig(true, false, 100, System.TimeSpan.FromMinutes(5)));
        hub.TakeSnapshot();
        return new PerformanceBundleService(hub, NewDir(), NullLogger.Instance);
    }

    private static EngineDispatchContext Ctx(string uri, PerformanceBundleService? svc, JsonElement? paramsEl = null)
    {
        var request = new ControlRequest("1", uri, paramsEl);
        return new EngineDispatchContext(request, perfBundles: svc);
    }

    [Fact]
    public async Task Create_ReturnsBundleDto()
    {
        var svc = NewService();
        var resp = await PerfBundleCommands.Create(Ctx("cove://commands/perf.bundle.create", svc));

        Assert.True(resp.Ok);
        var dto = resp.Data!.Value.Deserialize(CoveJsonContext.Default.PerfBundleDto)!;
        Assert.False(string.IsNullOrEmpty(dto.Id));
        Assert.True(System.IO.File.Exists(dto.BundlePath));
        Assert.False(dto.ContainsTrace);
    }

    [Fact]
    public async Task Create_NoService_Fails()
    {
        var resp = await PerfBundleCommands.Create(Ctx("cove://commands/perf.bundle.create", svc: null));
        Assert.False(resp.Ok);
        Assert.Equal("not_ready", resp.Error!.Code);
    }

    [Fact]
    public async Task List_ReturnsCreatedBundles()
    {
        var svc = NewService();
        svc.CreateBundle();
        svc.CreateBundle();
        var resp = await PerfBundleCommands.List(Ctx("cove://commands/perf.bundle.list", svc));

        Assert.True(resp.Ok);
        var dto = resp.Data!.Value.Deserialize(CoveJsonContext.Default.PerfBundleListResult)!;
        Assert.Equal(2, dto.Bundles.Count);
    }

    [Fact]
    public async Task List_NoService_Fails()
    {
        var resp = await PerfBundleCommands.List(Ctx("cove://commands/perf.bundle.list", svc: null));
        Assert.False(resp.Ok);
        Assert.Equal("not_ready", resp.Error!.Code);
    }

    [Fact]
    public async Task Delete_RemovesBundle()
    {
        var svc = NewService();
        var bundle = svc.CreateBundle();
        var p = JsonSerializer.SerializeToElement(new PerfBundleDeleteParams(bundle.BundlePath), CoveJsonContext.Default.PerfBundleDeleteParams);
        var resp = await PerfBundleCommands.Delete(Ctx("cove://commands/perf.bundle.delete", svc, p));

        Assert.True(resp.Ok);
        Assert.False(System.IO.File.Exists(bundle.BundlePath));
    }

    [Fact]
    public async Task Delete_Missing_Fails()
    {
        var svc = NewService();
        var p = JsonSerializer.SerializeToElement(new PerfBundleDeleteParams(System.IO.Path.Combine(NewDir(), "nope.zip")), CoveJsonContext.Default.PerfBundleDeleteParams);
        var resp = await PerfBundleCommands.Delete(Ctx("cove://commands/perf.bundle.delete", svc, p));

        Assert.False(resp.Ok);
        Assert.Equal("not_found", resp.Error!.Code);
    }

    [Fact]
    public async Task Delete_NoParams_InvalidParams()
    {
        var svc = NewService();
        var resp = await PerfBundleCommands.Delete(Ctx("cove://commands/perf.bundle.delete", svc));

        Assert.False(resp.Ok);
        Assert.Equal("invalid_params", resp.Error!.Code);
    }

    [Fact]
    public async Task Delete_EmptyPath_InvalidParams()
    {
        var svc = NewService();
        var p = JsonSerializer.SerializeToElement(new PerfBundleDeleteParams(""), CoveJsonContext.Default.PerfBundleDeleteParams);
        var resp = await PerfBundleCommands.Delete(Ctx("cove://commands/perf.bundle.delete", svc, p));

        Assert.False(resp.Ok);
        Assert.Equal("invalid_params", resp.Error!.Code);
    }

    [Fact]
    public async Task Delete_NoService_Fails()
    {
        var p = JsonSerializer.SerializeToElement(new PerfBundleDeleteParams("x.zip"), CoveJsonContext.Default.PerfBundleDeleteParams);
        var resp = await PerfBundleCommands.Delete(Ctx("cove://commands/perf.bundle.delete", svc: null, p));
        Assert.False(resp.Ok);
        Assert.Equal("not_ready", resp.Error!.Code);
    }
}
