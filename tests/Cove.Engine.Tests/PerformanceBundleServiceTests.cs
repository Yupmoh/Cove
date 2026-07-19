using Cove.Engine.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class PerformanceBundleServiceTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-perf-{System.Guid.NewGuid():N}");

    [Fact]
    public void CreateBundle_ProducesZipWithSnapshots()
    {
        var dir = NewDir();
        var hub = new DiagnosticsHub(new DiagnosticsConfig(true, false, 100, TimeSpan.FromMinutes(5)));
        hub.TakeSnapshot();
        hub.TakeSnapshot();
        var service = new PerformanceBundleService(hub, dir, NullLogger.Instance);

        var bundle = service.CreateBundle();

        Assert.True(File.Exists(bundle.BundlePath));
        Assert.Equal(2, bundle.SnapshotCount);
        Assert.False(bundle.ContainsTrace);
        Assert.True(bundle.SizeBytes > 0);
    }

    [Fact]
    public void CreateBundle_IncludesTraceWhenProvided()
    {
        var dir = NewDir();
        var hub = new DiagnosticsHub(new DiagnosticsConfig(true, false, 100, TimeSpan.FromMinutes(5)));
        hub.TakeSnapshot();
        var service = new PerformanceBundleService(hub, dir, NullLogger.Instance);

        var tracePath = Path.Combine(dir, "trace.nettrace");
        File.WriteAllBytes(tracePath, new byte[] { 1, 2, 3, 4 });

        var bundle = service.CreateBundle(tracePath);

        Assert.True(bundle.ContainsTrace);
        Assert.True(File.Exists(bundle.BundlePath));

        using var archive = System.IO.Compression.ZipFile.OpenRead(bundle.BundlePath);
        Assert.NotNull(archive.GetEntry("trace.nettrace"));
    }

    [Fact]
    public void ListBundles_ReturnsCreatedBundles()
    {
        var dir = NewDir();
        var hub = new DiagnosticsHub(new DiagnosticsConfig(true, false, 100, TimeSpan.FromMinutes(5)));
        hub.TakeSnapshot();
        var service = new PerformanceBundleService(hub, dir, NullLogger.Instance);

        service.CreateBundle();
        service.CreateBundle();

        var bundles = service.ListBundles();
        Assert.Equal(2, bundles.Count);
    }

    [Fact]
    public void ListBundles_OrdersByCreatedAtDescending()
    {
        var dir = NewDir();
        var hub = new DiagnosticsHub(new DiagnosticsConfig(true, false, 100, TimeSpan.FromMinutes(5)));
        var time = new ManualTimeProvider();
        var service = new PerformanceBundleService(hub, dir, NullLogger.Instance, time);

        var first = service.CreateBundle();
        time.Advance(TimeSpan.FromMilliseconds(1));
        var second = service.CreateBundle();

        var bundles = service.ListBundles();
        Assert.Equal(second.BundlePath, bundles[0].BundlePath);
        Assert.Equal(first.BundlePath, bundles[1].BundlePath);
    }

    [Fact]
    public void DeleteBundle_RemovesFile()
    {
        var dir = NewDir();
        var hub = new DiagnosticsHub(new DiagnosticsConfig(true, false, 100, TimeSpan.FromMinutes(5)));
        hub.TakeSnapshot();
        var service = new PerformanceBundleService(hub, dir, NullLogger.Instance);

        var bundle = service.CreateBundle();
        Assert.True(File.Exists(bundle.BundlePath));

        var deleted = service.DeleteBundle(bundle.BundlePath);
        Assert.True(deleted);
        Assert.False(File.Exists(bundle.BundlePath));
    }

    [Fact]
    public void DeleteBundle_ReturnsFalseForMissingFile()
    {
        var dir = NewDir();
        var hub = new DiagnosticsHub(new DiagnosticsConfig(true, false, 100, TimeSpan.FromMinutes(5)));
        var service = new PerformanceBundleService(hub, dir, NullLogger.Instance);

        Assert.False(service.DeleteBundle(Path.Combine(dir, "nonexistent.zip")));
    }
}
