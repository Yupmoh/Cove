using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cove.Platform.Pty;
using Cove.Platform.Pty.Unix;
using Cove.Testing;
using Microsoft.Extensions.Logging;
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

    [Fact]
    public void AdoptedSession_WatcherConstructionFailureIsLoggedAndRetained()
    {
        var logger = new CaptureLogger();
        var policy = new UnixPtyExitPolicy(
            TimeSpan.Zero,
            (_, _) => throw new PtyIoException("exit watcher creation failed (errno 24).", 24));
        using var session = new UnixPtySession(44, -1, 1234, logger, adopted: true, policy);

        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Error
                     && entry.Message.Contains("watcher creation failed", StringComparison.Ordinal)
                     && entry.Exception is PtyIoException { Errno: 24 });

        Assert.Equal(-1, session.WaitForExit());
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Error
                     && entry.Message.Contains("errno 24", StringComparison.Ordinal)
                     && entry.Exception is PtyIoException { Errno: 24 });
    }

    [Fact]
    public void AdoptedSession_UsesInjectedTimeoutWithoutPublishingAnExit()
    {
        var logger = new CaptureLogger();
        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var policy = new UnixPtyExitPolicy(TimeSpan.Zero, (_, _) => pending.Task);
        using var session = new UnixPtySession(45, -1, 1234, logger, adopted: true, policy);

        Assert.Equal(-1, session.WaitForExit());
        Assert.False(session.HasExited);
        Assert.Equal(-1, session.ExitCode);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Warning
                     && entry.Message.Contains("timed out", StringComparison.Ordinal));
    }

    [Fact]
    public void AdoptedSession_CanceledObservationIsLoggedWithDetail()
    {
        var logger = new CaptureLogger();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var policy = new UnixPtyExitPolicy(
            TimeSpan.FromSeconds(1),
            (_, _) => Task.FromCanceled<int>(cancellation.Token));
        using var session = new UnixPtySession(46, -1, 1234, logger, adopted: true, policy);

        Assert.Equal(-1, session.WaitForExit());
        Assert.False(session.HasExited);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Warning
                     && entry.Message.Contains("canceled", StringComparison.Ordinal)
                     && entry.Exception is OperationCanceledException);
    }

    [Fact]
    public void AdoptedSession_NativeWaitFailureRetainsActionableDetail()
    {
        var logger = new CaptureLogger();
        var nativeFailure = new PtyIoException("exit watcher loop failed (errno 5).", 5);
        var policy = new UnixPtyExitPolicy(
            TimeSpan.FromSeconds(1),
            (_, _) => Task.FromException<int>(nativeFailure));
        using var session = new UnixPtySession(47, -1, 1234, logger, adopted: true, policy);

        Assert.Equal(-1, session.WaitForExit());
        Assert.False(session.HasExited);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Error
                     && entry.Message.Contains("errno 5", StringComparison.Ordinal)
                     && ReferenceEquals(entry.Exception, nativeFailure));
    }

    [Fact]
    public void AdoptedSession_LateExitCanBePublishedAfterTimeout()
    {
        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var policy = new UnixPtyExitPolicy(TimeSpan.Zero, (_, _) => pending.Task);
        using var session = new UnixPtySession(48, -1, 1234, NullLogger.Instance, adopted: true, policy);

        Assert.Equal(-1, session.WaitForExit());
        Assert.False(session.HasExited);

        pending.SetResult(9 << 8);

        Assert.Equal(9, session.WaitForExit());
        Assert.True(session.HasExited);
        Assert.Equal(9, session.ExitCode);
    }

    private sealed class CaptureLogger : ILogger
    {
        internal List<Entry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add(new Entry(logLevel, formatter(state, exception), exception));

        internal sealed record Entry(LogLevel Level, string Message, Exception? Exception);
    }
}
