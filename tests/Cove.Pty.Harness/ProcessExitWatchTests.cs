using System.Diagnostics;
using Cove.Platform.Pty.Unix;
using Xunit;

namespace Cove.Pty.Harness;

public sealed class ProcessExitWatchTests
{
    private static int SpawnOrphan(string command)
    {
        var psi = new ProcessStartInfo("/bin/sh")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add($"{command} & echo $!");
        using var proc = Process.Start(psi)!;
        var line = proc.StandardOutput.ReadLine();
        proc.WaitForExit(5000);
        return int.Parse(line!.Trim());
    }

    [Fact]
    public async Task WaitForExitAsync_ObservesNonChildExit()
    {
        if (OperatingSystem.IsWindows()) return;
        var pid = SpawnOrphan("sleep 0.3");
        await ProcessExitWatch.WaitForExitAsync(pid).WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task WaitForExitAsync_AlreadyDeadPid_CompletesImmediately()
    {
        if (OperatingSystem.IsWindows()) return;
        var pid = SpawnOrphan("true");
        var deadline = Stopwatch.StartNew();
        while (deadline.ElapsedMilliseconds < 5000)
        {
            try { Process.GetProcessById(pid); await Task.Delay(20); }
            catch (ArgumentException) { break; }
        }
        await ProcessExitWatch.WaitForExitAsync(pid).WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task WaitForExitAsync_LivePid_DoesNotComplete()
    {
        if (OperatingSystem.IsWindows()) return;
        var pid = SpawnOrphan("sleep 30");
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(
                () => ProcessExitWatch.WaitForExitAsync(pid).WaitAsync(TimeSpan.FromMilliseconds(300)));
        }
        finally
        {
            try { Process.GetProcessById(pid).Kill(); } catch (ArgumentException) { }
        }
    }

    [Fact]
    public async Task WaitForExitAsync_ManyConcurrentWatches_AllComplete()
    {
        if (OperatingSystem.IsWindows()) return;
        var pids = Enumerable.Range(0, 8).Select(_ => SpawnOrphan("sleep 0.2")).ToArray();
        var waits = pids.Select(p => ProcessExitWatch.WaitForExitAsync(p)).ToArray();
        await Task.WhenAll(waits).WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task WaitForExitAsync_SamePidTwice_BothObserversComplete()
    {
        if (OperatingSystem.IsWindows()) return;
        var pid = SpawnOrphan("sleep 0.3");
        var first = ProcessExitWatch.WaitForExitAsync(pid);
        var second = ProcessExitWatch.WaitForExitAsync(pid);
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task WaitForExitAsync_ReportsExitCode()
    {
        if (!OperatingSystem.IsMacOS()) return;
        var pid = SpawnOrphan("{ sleep 0.3; exit 7; }");
        var status = await ProcessExitWatch.WaitForExitAsync(pid).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(7, ProcessExitWatch.DecodeWaitStatus(status));
    }

    [Fact]
    public async Task WaitForExitAsync_ReportsFatalSignal()
    {
        if (!OperatingSystem.IsMacOS()) return;
        var pid = SpawnOrphan("sleep 30");
        var wait = ProcessExitWatch.WaitForExitAsync(pid);
        Process.Start("/bin/kill", new[] { "-9", pid.ToString() })!.WaitForExit();
        var status = await wait.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(137, ProcessExitWatch.DecodeWaitStatus(status));
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
}
