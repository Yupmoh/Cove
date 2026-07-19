using Cove.Engine.Daemon;
using Cove.Platform;
using Cove.Platform.Ipc;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class DaemonDataDirOwnershipTests
{
    [Fact]
    public async Task DifferentChannel_SecondInstance_ExitsWithoutTouchingSharedState()
    {
        var parent = Path.Combine(Path.GetTempPath(), "cove-owner-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        await using var environment = await ProcessEnvironmentScope.SetAsync("COVE_DATA_DIR", parent);
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
            Cove.Testing.TestDirectory.Delete(parent);
        }
    }

    [Fact]
    public async Task SameChannel_SecondInstance_ExitsNonzero()
    {
        var parent = Path.Combine(Path.GetTempPath(), "cove-owner-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        await using var environment = await ProcessEnvironmentScope.SetAsync("COVE_DATA_DIR", parent);
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
            Cove.Testing.TestDirectory.Delete(parent);
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task CleanShutdown_ReleasesDataDirLock_SubsequentBootSucceeds()
    {
        var parent = Path.Combine(Path.GetTempPath(), "cove-owner-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        ProcessEnvironmentScope? environment = null;
        CancellationTokenSource? cts = null;
        DaemonHost? host = null;
        Task<int>? run = null;
        string? daemonLockPath = null;
        var failures = new List<Exception>();
        try
        {
            environment = await ProcessEnvironmentScope.SetAsync("COVE_DATA_DIR", parent);
            var dd = CoveDataDir.Resolve(CoveChannel.Dev);
            var paths = new DaemonPaths(dd);
            daemonLockPath = paths.DaemonLockPath;
            var endpoint = ControlEndpointFactory.FromSocketPath(dd.SocketPath);
            host = new DaemonHost(paths, endpoint, exitWhenIdle: false);
            cts = new CancellationTokenSource();
            run = Task.Run(() => host.RunAsync(cts.Token));

            await AsyncTest.EventuallyAsync(
                () => endpoint.TryProbe(100),
                TimeSpan.FromSeconds(10),
                "daemon endpoint did not become ready");

            var contended = SingleInstanceGuard.TryAcquire(paths.DaemonLockPath);
            Assert.Null(contended);
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }
        finally
        {
            if (cts is not null)
            {
                try
                {
                    cts.Cancel();
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }
            }

            if (run is not null)
            {
                try
                {
                    await run.WaitAsync(TimeSpan.FromSeconds(10));
                }
                catch (TimeoutException ex)
                {
                    failures.Add(new TimeoutException(
                        "daemon did not terminate within 10 seconds after clean-shutdown cancellation",
                        ex));
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }
            }

            if (run is not null && !run.IsCompleted && host is not null)
            {
                try
                {
                    host.RequestHardStop();
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }

                try
                {
                    await run.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException ex)
                {
                    failures.Add(new TimeoutException(
                        "daemon did not terminate within 5 seconds after clean-shutdown hard stop",
                        ex));
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }
            }

            if (run is null || run.IsCompleted)
            {
                try
                {
                    using var reacquired = SingleInstanceGuard.TryAcquire(
                        daemonLockPath
                            ?? throw new InvalidOperationException(
                                "daemon lock path was not initialized"));
                    Assert.NotNull(reacquired);
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }

                try
                {
                    if (environment is not null)
                        await environment.DisposeAsync();
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }

                try
                {
                    Cove.Testing.TestDirectory.Delete(parent);
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }

                try
                {
                    cts?.Dispose();
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }
            }
        }

        if (failures.Count > 0)
            throw new AggregateException(
                "clean shutdown ownership test failed",
                failures);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task HarnessDispose_GracefulTimeout_HardStopsBeforeCleanup()
    {
        string? parent = null;
        try
        {
            var first = await DaemonTestHarness.StartAsync(
                lifecycle: new DaemonTestHarness.LifecycleOptions
                {
                    GracefulStopTimeout = TimeSpan.FromMilliseconds(100),
                    HardStopTimeout = TimeSpan.FromSeconds(5),
                    RequestGracefulStop = static _ => { }
                });
            parent = first.DataDir;
            var firstRun = first.Run;

            var failure = await Assert.ThrowsAsync<AggregateException>(
                () => first.DisposeAsync().AsTask());
            var repeatedFailure = await Assert.ThrowsAsync<AggregateException>(
                () => first.DisposeAsync().AsTask());

            Assert.True(firstRun.IsCompleted);
            Assert.Same(failure, repeatedFailure);
            Assert.False(Directory.Exists(parent));
            Assert.Contains(
                failure.Flatten().InnerExceptions,
                ex => ex is TimeoutException
                    && ex.Message.Contains(
                        "after graceful stop",
                        StringComparison.Ordinal));

            var second = await DaemonTestHarness.StartAsync(dataDir: parent);
            await second.DisposeAsync();
            Assert.True(second.Run.IsCompleted);
        }
        finally
        {
            if (parent is not null)
                Cove.Testing.TestDirectory.Delete(parent);
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task StartAsync_ReadinessFailure_PreservesCleanupFailuresAndReleasesDaemon()
    {
        var parent = Path.Combine(
            Path.GetTempPath(),
            "cove-owner-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var failure = await Assert.ThrowsAsync<AggregateException>(
                () => DaemonTestHarness.StartAsync(
                    dataDir: parent,
                    lifecycle: new DaemonTestHarness.LifecycleOptions
                    {
                        ReadinessTimeout = TimeSpan.FromMilliseconds(100),
                        GracefulStopTimeout = TimeSpan.FromMilliseconds(100),
                        HardStopTimeout = TimeSpan.FromSeconds(5),
                        ReadinessProbe = static _ => false,
                        RequestGracefulStop = static _ =>
                            throw new InvalidOperationException(
                                "forced graceful cleanup failure"),
                        RequestHardStop = static host =>
                        {
                            host.RequestHardStop();
                            throw new InvalidOperationException(
                                "forced hard-stop cleanup failure");
                        }
                    }));

            var flattened = failure.Flatten().InnerExceptions;
            Assert.Contains(
                flattened,
                ex => ex.Message.Contains(
                    "daemon did not become connectable",
                    StringComparison.Ordinal));
            Assert.Contains(
                flattened,
                ex => ex.Message == "forced graceful cleanup failure");
            Assert.Contains(
                flattened,
                ex => ex.Message == "forced hard-stop cleanup failure");

            var second = await DaemonTestHarness.StartAsync(dataDir: parent);
            await second.DisposeAsync();
            Assert.True(second.Run.IsCompleted);
        }
        finally
        {
            Cove.Testing.TestDirectory.Delete(parent);
        }
    }
}
