using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Cove.Platform.Pty.Unix;
using Cove.Testing;
using Xunit;

namespace Cove.Pty.Harness;

public sealed class ProcessExitWatchTests
{
    private static OrphanProcess SpawnOrphan(string command)
    {
        var psi = new ProcessStartInfo("/bin/sh")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add($"{command} & echo $!");
        using var proc = Process.Start(psi)!;
        string? line;
        try
        {
            line = proc.StandardOutput.ReadLineAsync()
                .WaitAsync(TimeSpan.FromSeconds(5))
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception failure)
        {
            Exception? cleanupFailure = null;
            try
            {
                if (!proc.HasExited)
                    proc.Kill(entireProcessTree: true);
                if (!proc.WaitForExit(5000))
                    throw new TimeoutException("orphan launcher did not exit within 5 seconds after kill");
            }
            catch (Exception exception)
            {
                cleanupFailure = exception;
            }
            if (cleanupFailure is not null)
                throw new AggregateException(failure, cleanupFailure);
            ExceptionDispatchInfo.Capture(failure).Throw();
            throw;
        }
        if (!proc.WaitForExit(5000))
        {
            var timeout = new TimeoutException("orphan launcher did not exit within 5 seconds");
            try
            {
                proc.Kill(entireProcessTree: true);
                if (!proc.WaitForExit(5000))
                    throw new TimeoutException("orphan launcher did not exit within 5 seconds after kill");
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(timeout, cleanupFailure);
            }
            throw timeout;
        }
        Assert.Equal(0, proc.ExitCode);
        var pid = int.Parse(line!.Trim());
        try
        {
            using var orphan = Process.GetProcessById(pid);
            return new OrphanProcess(pid, orphan.StartTime.ToUniversalTime());
        }
        catch (ArgumentException)
        {
            return new OrphanProcess(pid, null);
        }
        catch (InvalidOperationException)
        {
            return new OrphanProcess(pid, null);
        }
        catch (Win32Exception)
        {
            return new OrphanProcess(pid, null);
        }
    }

    private static async Task KillIfRunningAsync(OrphanProcess orphan, Task<int>? observedExit)
    {
        if (observedExit?.IsCompletedSuccessfully == true || orphan.StartTimeUtc is null)
            return;

        try
        {
            using var process = Process.GetProcessById(orphan.Pid);
            if (process.StartTime.ToUniversalTime() != orphan.StartTimeUtc)
                return;
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException($"orphan process {orphan.Pid} did not exit within 5 seconds after kill", exception);
        }
    }

    private static async Task RunNativeExitWatchTestAsync(
        nint watch,
        OrphanProcess orphan,
        Func<Action<Task<(long Token, int Status)>>, Task> body,
        Action<Task<(long Token, int Status)>?>? beforeFree = null)
    {
        Task<(long Token, int Status)>? worker = null;
        Exception? primaryFailure = null;
        try
        {
            await body(task => worker = task);
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
        }

        var cleanupFailures = new List<Exception>();
        try
        {
            beforeFree?.Invoke(worker);
        }
        catch (Exception exception)
        {
            cleanupFailures.Add(exception);
        }

        try
        {
            CovePtyNative.ExitWatchFree(watch);
        }
        catch (Exception exception)
        {
            cleanupFailures.Add(exception);
        }

        if (worker is not null)
        {
            try
            {
                await worker.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(exception);
            }
        }

        try
        {
            await KillIfRunningAsync(orphan, null);
        }
        catch (Exception exception)
        {
            cleanupFailures.Add(exception);
        }

        if (primaryFailure is not null)
        {
            if (cleanupFailures.Count > 0)
                throw new AggregateException(new[] { primaryFailure }.Concat(cleanupFailures));
            ExceptionDispatchInfo.Capture(primaryFailure).Throw();
        }

        if (cleanupFailures.Count == 1)
            ExceptionDispatchInfo.Capture(cleanupFailures[0]).Throw();
        if (cleanupFailures.Count > 1)
            throw new AggregateException(cleanupFailures);
    }

    [PlatformFact(TestOperatingSystem.MacOS)]
    public async Task WaitForExitAsync_ObservesNonChildExitWithDecodedStatus()
    {
        var orphan = SpawnOrphan("sleep 0.3");
        Task<int>? wait = null;
        try
        {
            wait = ProcessExitWatch.WaitForExitAsync(orphan.Pid);
            var status = await wait.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(0, ProcessExitWatch.DecodeWaitStatus(status));
        }
        finally
        {
            await KillIfRunningAsync(orphan, wait);
        }
    }

    [PlatformFact(TestOperatingSystem.Linux)]
    public async Task WaitForExitAsync_ObservesNonChildExitWithUnknownStatus()
    {
        var orphan = SpawnOrphan("sleep 0.3");
        Task<int>? wait = null;
        try
        {
            wait = ProcessExitWatch.WaitForExitAsync(orphan.Pid);
            var status = await wait.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(-1, status);
        }
        finally
        {
            await KillIfRunningAsync(orphan, wait);
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task WaitForExitAsync_AlreadyDeadPid_CompletesImmediately()
    {
        var orphan = SpawnOrphan("true");
        Task<int>? wait = null;
        try
        {
            await AsyncTest.EventuallyAsync(
                () =>
                {
                    try
                    {
                        using var _ = Process.GetProcessById(orphan.Pid);
                        return false;
                    }
                    catch (ArgumentException)
                    {
                        return true;
                    }
                },
                TimeSpan.FromSeconds(5),
                $"orphan process {orphan.Pid} remained alive");
            wait = ProcessExitWatch.WaitForExitAsync(orphan.Pid);
            await wait.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            await KillIfRunningAsync(orphan, wait);
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task WaitForExitAsync_LivePid_DoesNotComplete()
    {
        var orphan = SpawnOrphan("sleep 30");
        Task<int>? wait = null;
        try
        {
            wait = ProcessExitWatch.WaitForExitAsync(orphan.Pid);
            await Assert.ThrowsAsync<TimeoutException>(
                () => wait.WaitAsync(TimeSpan.FromMilliseconds(300)));
        }
        finally
        {
            await KillIfRunningAsync(orphan, wait);
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task WaitForExitAsync_ManyConcurrentWatches_AllComplete()
    {
        var orphans = new List<OrphanProcess>();
        var waits = new Dictionary<int, Task<int>>();
        try
        {
            for (var i = 0; i < 8; i++)
            {
                var orphan = SpawnOrphan("sleep 0.2");
                orphans.Add(orphan);
                waits.Add(orphan.Pid, ProcessExitWatch.WaitForExitAsync(orphan.Pid));
            }
            await Task.WhenAll(waits.Values).WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await Task.WhenAll(orphans.Select(orphan => KillIfRunningAsync(orphan, waits.GetValueOrDefault(orphan.Pid))));
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task WaitForExitAsync_SamePidTwice_BothObserversComplete()
    {
        var orphan = SpawnOrphan("sleep 0.3");
        Task<int>? first = null;
        Task<int>? second = null;
        try
        {
            first = ProcessExitWatch.WaitForExitAsync(orphan.Pid);
            second = ProcessExitWatch.WaitForExitAsync(orphan.Pid);
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await KillIfRunningAsync(orphan, first?.IsCompletedSuccessfully == true ? first : second);
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task WaitForExitAsync_CanceledObserverReleasesRegistration()
    {
        var orphan = SpawnOrphan("sleep 30");
        Task<int>? replacement = null;
        using var cancellation = new CancellationTokenSource();
        try
        {
            var canceled = ProcessExitWatch.WaitForExitAsync(orphan.Pid, cancellation.Token);
            await AsyncTest.EventuallyAsync(
                () => ProcessExitWatch.IsRegistered(orphan.Pid),
                TimeSpan.FromSeconds(2),
                $"exit watch for pid {orphan.Pid} was not registered");

            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => canceled.WaitAsync(TimeSpan.FromSeconds(2)));
            await AsyncTest.EventuallyAsync(
                () => !ProcessExitWatch.IsRegistered(orphan.Pid),
                TimeSpan.FromSeconds(2),
                $"canceled exit watch for pid {orphan.Pid} remained registered");

            replacement = ProcessExitWatch.WaitForExitAsync(orphan.Pid);
            await KillIfRunningAsync(orphan, null);
            await replacement.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await KillIfRunningAsync(orphan, replacement);
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task NativeExitWatch_TimeoutFreeWakesAndJoinsBlockedWorker()
    {
        var orphan = SpawnOrphan("sleep 30");
        var watch = CovePtyNative.ExitWatchNew();
        Assert.True(watch > 0);
        var workerWasBlockedBeforeFree = false;
        Task<(long Token, int Status)>? blockedWorker = null;

        var forcedTimeout = await Assert.ThrowsAsync<TimeoutException>(
            () => RunNativeExitWatchTestAsync(
                watch,
                orphan,
                setWorker =>
                {
                    Assert.Equal(0, CovePtyNative.ExitWatchAdd(watch, orphan.Pid, 101));
                    var worker = Task.Run(
                        () =>
                        {
                            var token = CovePtyNative.ExitWatchNext(watch, out var status);
                            return (Token: token, Status: status);
                        });
                    blockedWorker = worker;
                    setWorker(worker);
                    var entered = CovePtyNative.ExitWatchWaitReaderEntered(watch);
                    Assert.Equal(0, entered);
                    return Task.FromException(
                        new TimeoutException("forced timeout after native reader entry"));
                },
                worker =>
                {
                    Assert.NotNull(worker);
                    Assert.False(worker.IsCompleted);
                    workerWasBlockedBeforeFree = true;
                }));

        Assert.Equal("forced timeout after native reader entry", forcedTimeout.Message);
        Assert.True(workerWasBlockedBeforeFree);
        Assert.NotNull(blockedWorker);
        Assert.True(blockedWorker.IsCompletedSuccessfully);
        Assert.True(blockedWorker.Result.Token < 0);
        Assert.Equal(-1, blockedWorker.Result.Status);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task NativeExitWatch_RemovedPidGenerationCannotCompleteReplacement()
    {
        var orphan = SpawnOrphan("sleep 30");
        var watch = CovePtyNative.ExitWatchNew();
        Assert.True(watch > 0);
        const long oldToken = 101;
        const long replacementToken = 202;
        await RunNativeExitWatchTestAsync(
            watch,
            orphan,
            async setWorker =>
            {
                Assert.Equal(0, CovePtyNative.ExitWatchAdd(watch, orphan.Pid, oldToken));
                Assert.Equal(0, CovePtyNative.ExitWatchRemove(watch, oldToken));
                Assert.Equal(0, CovePtyNative.ExitWatchAdd(watch, orphan.Pid, replacementToken));

                await KillIfRunningAsync(orphan, null);
                var worker = Task.Run(
                    () =>
                    {
                        var token = CovePtyNative.ExitWatchNext(watch, out var status);
                        return (Token: token, Status: status);
                    });
                setWorker(worker);
                var observed = await worker.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.Equal(replacementToken, observed.Token);
                if (OperatingSystem.IsLinux())
                    Assert.Equal(-1, observed.Status);
                else
                    Assert.Equal(137, ProcessExitWatch.DecodeWaitStatus(observed.Status));
            });
    }

    [PlatformFact(TestOperatingSystem.MacOS)]
    public async Task WaitForExitAsync_ReportsExitCode()
    {
        var orphan = SpawnOrphan("{ sleep 0.3; exit 7; }");
        Task<int>? wait = null;
        try
        {
            wait = ProcessExitWatch.WaitForExitAsync(orphan.Pid);
            var status = await wait.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(7, ProcessExitWatch.DecodeWaitStatus(status));
        }
        finally
        {
            await KillIfRunningAsync(orphan, wait);
        }
    }

    [PlatformFact(TestOperatingSystem.MacOS)]
    public async Task WaitForExitAsync_ReportsFatalSignal()
    {
        var orphan = SpawnOrphan("sleep 30");
        Task<int>? wait = null;
        try
        {
            wait = ProcessExitWatch.WaitForExitAsync(orphan.Pid);
            using var killer = Process.Start("/bin/kill", new[] { "-9", orphan.Pid.ToString() })!;
            Assert.Equal(0, await TestProcess.WaitForExitAsync(killer, TimeSpan.FromSeconds(5)));
            var status = await wait.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(137, ProcessExitWatch.DecodeWaitStatus(status));
        }
        finally
        {
            await KillIfRunningAsync(orphan, wait);
        }
    }

    [Fact]
    public void DecodeWaitStatus_MapsShapes()
    {
        Assert.Equal(0, ProcessExitWatch.DecodeWaitStatus(0));
        Assert.Equal(7, ProcessExitWatch.DecodeWaitStatus(7 << 8));
        Assert.Equal(137, ProcessExitWatch.DecodeWaitStatus(9));
        Assert.Equal(-1, ProcessExitWatch.DecodeWaitStatus(-1));
        Assert.Equal(-1, ProcessExitWatch.DecodeWaitStatus(0x7f));
    }

    private sealed record OrphanProcess(int Pid, DateTime? StartTimeUtc);
}
