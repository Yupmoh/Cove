using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Cove.Platform.Pty;
using Cove.Platform.Pty.Unix;
using Cove.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Pty.Harness;

public sealed class UnixPtyExitStateTests
{
    [PlatformFact(TestOperatingSystem.MacOS)]
    public async Task WaitForExit_DoesNotPublishExitUntilLiveChildActuallyExits()
    {
        var host = new UnixPtyHost(NullLogger.Instance);
        using var session = host.Spawn(new PtySpawnRequest
        {
            Command = "/bin/sh",
            Args = new[] { "-c", "trap 'exit 23' USR1; printf 'READY\\n'; while :; do read _; done" },
        });

        var output = new StringBuilder();
        var buffer = new byte[256];
        var readyDeadline = Stopwatch.StartNew();
        while (!output.ToString().Contains("READY", StringComparison.Ordinal)
               && readyDeadline.Elapsed < TimeSpan.FromSeconds(10))
        {
            if (!session.WaitReadable(250))
                continue;
            var count = session.Read(buffer);
            if (count == 0)
                break;
            output.Append(Encoding.UTF8.GetString(buffer, 0, count));
        }
        Assert.Contains("READY", output.ToString());

        var wait = Task.Run(session.WaitForExit);
        int exitCode;
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(
                () => wait.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.False(session.HasExited);
            Assert.Equal(-1, session.ExitCode);
            Assert.False(wait.IsCompleted);

            Assert.True(session.Signal(PtyConstants.SigUsr1));
            exitCode = await AsyncTest.CompletesWithinAsync(
                wait,
                TimeSpan.FromSeconds(10),
                "PTY child did not exit after SIGUSR1");
        }
        finally
        {
            if (!wait.IsCompleted)
            {
                session.Kill();
                await AsyncTest.CompletesWithinAsync(
                    wait,
                    TimeSpan.FromSeconds(10),
                    "PTY child did not exit during test cleanup");
            }
        }

        Assert.Equal(23, exitCode);
        Assert.True(session.HasExited);
        Assert.Equal(23, session.ExitCode);
    }

    [PlatformFact(TestOperatingSystem.MacOS)]
    public void WaitReadable_AfterDisposeThrowsInsteadOfReportingReadable()
    {
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

    [PlatformFact(TestOperatingSystem.MacOS)]
    public void WaitReadable_InvalidDescriptorThrowsWithErrno()
    {
        using var session = new UnixPtySession(42, 1_000_000, Environment.ProcessId, NullLogger.Instance);

        var exception = Assert.Throws<PtyIoException>(() => session.WaitReadable(0));

        Assert.Equal(9, exception.Errno);
    }

    [PlatformFact(TestOperatingSystem.MacOS)]
    public void WaitForExit_WaitPidFailureLeavesObservationUnknown()
    {
        using var session = new UnixPtySession(43, -1, Environment.ProcessId, NullLogger.Instance);

        Assert.Equal(-1, session.WaitForExit());
        Assert.False(session.HasExited);
        Assert.Equal(-1, session.ExitCode);
    }

    [PlatformFact(TestOperatingSystem.MacOS)]
    public void NativeReap_PreservesWaitPidErrno()
    {
        var rc = CovePtyNative.Reap(Environment.ProcessId);

        Assert.Equal(-10, rc);
    }
}
