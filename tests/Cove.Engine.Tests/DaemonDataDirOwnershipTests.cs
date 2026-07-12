using Cove.Engine.Daemon;
using Cove.Platform;
using Cove.Platform.Ipc;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class DaemonDataDirOwnershipTests
{
    [Fact]
    public async Task DifferentChannel_SecondInstance_ExitsWithoutTouchingSharedState()
    {
        var parent = Path.Combine(Path.GetTempPath(), "cove-owner-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        var prev = Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        Environment.SetEnvironmentVariable("COVE_DATA_DIR", parent);
        try
        {
            var ddStable = CoveDataDir.Resolve(CoveChannel.Stable);
            var stablePaths = new DaemonPaths(ddStable);
            var ddDev = CoveDataDir.Resolve(CoveChannel.Dev);
            var devPaths = new DaemonPaths(ddDev);
            Assert.Equal(stablePaths.DaemonLockPath, devPaths.DaemonLockPath);
            CoveTree.Ensure(ddDev);

            var portFile = Path.Combine(ddDev.Root, "hook-port");
            await File.WriteAllTextAsync(portFile, "51525");

            var binDir = Path.Combine(ddDev.Root, "bin");
            Directory.CreateDirectory(binDir);
            var linkPath = Path.Combine(binDir, OperatingSystem.IsWindows() ? "cove.exe" : "cove");
            await File.WriteAllTextAsync(linkPath, "seeded-cli-target");

            var statePath = Path.Combine(ddDev.Root, "state.json");
            await File.WriteAllTextAsync(statePath, "{\"seeded\":true}");
            var stateMtime = File.GetLastWriteTimeUtc(statePath);

            using var owner = SingleInstanceGuard.TryAcquire(stablePaths.DaemonLockPath);
            Assert.NotNull(owner);

            var endpoint = ControlEndpointFactory.FromSocketPath(ddDev.SocketPath);
            var host = new DaemonHost(devPaths, endpoint, exitWhenIdle: false);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var exit = await host.RunAsync(cts.Token);

            Assert.NotEqual(0, exit);
            Assert.Equal("51525", await File.ReadAllTextAsync(portFile));
            Assert.Equal("seeded-cli-target", await File.ReadAllTextAsync(linkPath));
            Assert.Equal("{\"seeded\":true}", await File.ReadAllTextAsync(statePath));
            Assert.Equal(stateMtime, File.GetLastWriteTimeUtc(statePath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("COVE_DATA_DIR", prev);
            try { Directory.Delete(parent, true); } catch { }
        }
    }

    [Fact]
    public async Task SameChannel_SecondInstance_ExitsNonzero()
    {
        var parent = Path.Combine(Path.GetTempPath(), "cove-owner-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        var prev = Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        Environment.SetEnvironmentVariable("COVE_DATA_DIR", parent);
        try
        {
            var dd = CoveDataDir.Resolve(CoveChannel.Dev);
            var paths = new DaemonPaths(dd);
            CoveTree.Ensure(dd);
            var portFile = Path.Combine(dd.Root, "hook-port");
            await File.WriteAllTextAsync(portFile, "51525");

            using var owner = SingleInstanceGuard.TryAcquire(paths.DaemonLockPath);
            Assert.NotNull(owner);

            var endpoint = ControlEndpointFactory.FromSocketPath(dd.SocketPath);
            var host = new DaemonHost(paths, endpoint, exitWhenIdle: false);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var exit = await host.RunAsync(cts.Token);

            Assert.NotEqual(0, exit);
            Assert.Equal("51525", await File.ReadAllTextAsync(portFile));
        }
        finally
        {
            Environment.SetEnvironmentVariable("COVE_DATA_DIR", prev);
            try { Directory.Delete(parent, true); } catch { }
        }
    }

    [Fact]
    public async Task CleanShutdown_ReleasesDataDirLock_SubsequentBootSucceeds()
    {
        if (OperatingSystem.IsWindows())
            return;
        var parent = Path.Combine(Path.GetTempPath(), "cove-owner-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        var prev = Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        Environment.SetEnvironmentVariable("COVE_DATA_DIR", parent);
        try
        {
            var dd = CoveDataDir.Resolve(CoveChannel.Dev);
            var paths = new DaemonPaths(dd);
            var endpoint = ControlEndpointFactory.FromSocketPath(dd.SocketPath);
            var host = new DaemonHost(paths, endpoint, exitWhenIdle: false);
            using var cts = new CancellationTokenSource();
            var run = Task.Run(() => host.RunAsync(cts.Token));

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < 10 && !endpoint.TryProbe(100))
                await Task.Delay(20);
            Assert.True(endpoint.TryProbe(100));

            var contended = SingleInstanceGuard.TryAcquire(paths.DaemonLockPath);
            Assert.Null(contended);

            cts.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(10));

            using var reacquired = SingleInstanceGuard.TryAcquire(paths.DaemonLockPath);
            Assert.NotNull(reacquired);
        }
        finally
        {
            Environment.SetEnvironmentVariable("COVE_DATA_DIR", prev);
            try { Directory.Delete(parent, true); } catch { }
        }
    }
}
