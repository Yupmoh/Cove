using System;
using System.Threading.Tasks;
using Cove.Platform.Pty;
using Cove.Platform.Pty.Unix;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Pty.Harness;

public sealed class UnixPtyExitStateTests
{
    [Fact]
    public async Task WaitForExit_DoesNotPublishExitUntilLiveChildActuallyExits()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var host = new UnixPtyHost(NullLogger.Instance);
        using var session = host.Spawn(new PtySpawnRequest
        {
            Command = "/bin/sh",
            Args = new[] { "-c", "sleep 4; exit 23" },
        });

        var wait = Task.Run(session.WaitForExit);
        await Task.Delay(TimeSpan.FromMilliseconds(2500));

        Assert.False(session.HasExited);
        Assert.Equal(-1, session.ExitCode);
        Assert.False(wait.IsCompleted);

        Assert.Equal(23, await wait.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(session.HasExited);
        Assert.Equal(23, session.ExitCode);
    }

    [Fact]
    public void WaitReadable_AfterDisposeThrowsInsteadOfReportingReadable()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var host = new UnixPtyHost(NullLogger.Instance);
        var session = host.Spawn(new PtySpawnRequest
        {
            Command = "/bin/sh",
            Args = new[] { "-c", "exit 0" },
        });

        Assert.Equal(0, session.WaitForExit());
        session.Dispose();

        Assert.Throws<ObjectDisposedException>(() => session.WaitReadable(0));
    }

    [Fact]
    public void WaitReadable_InvalidDescriptorThrowsWithErrno()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var session = new UnixPtySession(42, 1_000_000, Environment.ProcessId, NullLogger.Instance);

        var exception = Assert.Throws<PtyIoException>(() => session.WaitReadable(0));

        Assert.Equal(9, exception.Errno);
    }

    [Fact]
    public void WaitForExit_WaitPidFailureLeavesObservationUnknown()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var session = new UnixPtySession(43, -1, Environment.ProcessId, NullLogger.Instance);

        Assert.Equal(-1, session.WaitForExit());
        Assert.False(session.HasExited);
        Assert.Equal(-1, session.ExitCode);
    }

    [Fact]
    public void NativeReap_PreservesWaitPidErrno()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var rc = CovePtyNative.Reap(Environment.ProcessId);

        Assert.Equal(-10, rc);
    }
}
