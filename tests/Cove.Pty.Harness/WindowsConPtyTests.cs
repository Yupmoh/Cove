using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Cove.Platform.Pty.Windows;
using Cove.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace Cove.Pty.Harness;

public sealed class WindowsConPtyTests
{
    [Trait("Suite", "PtyInteractive")]
    [PlatformFact(TestOperatingSystem.Windows)]
    public void ConPtySpawnEchoesOutputAndExitsWithCodeZero()
    {
        var logger = NullLogger.Instance;
        var host = PtyHostFactory.Create(logger);
        var session = host.Spawn(new PtySpawnRequest
        {
            Command = "cmd.exe",
            Args = new[] { "/c", "echo hello" },
            Cols = 80,
            Rows = 24,
            Environment = new System.Collections.Generic.Dictionary<string, string>
            {
                ["COVE_NOOK_ID"] = "conpty-smoke",
            },
        });

        var ring = new PtyRingBuffer(1 << 20);
        var signal = new PtyRingSignal();
        var reader = new PtySessionReader(session, ring, signal, logger);
        reader.Start();
        try
        {
            session.Resize(100, 40);

            var sw = Stopwatch.StartNew();
            while (!reader.HasCompleted && sw.Elapsed.TotalSeconds < 15)
                Thread.Sleep(5);

            var cursor = new PtyClientCursor();
            var sink = new RecordingSink();
            var delivery = new PtyDelivery(session.SessionId, ring, cursor, sink);
            delivery.PumpAvailable();
            byte[] raw = sink.Delivered;
            string text = Encoding.UTF8.GetString(raw);
            string dump = DescribeCapture(raw, text);

            Assert.True(reader.HasCompleted, $"reader never completed. {dump}");
            Assert.True(reader.ExitCode == 0, $"reader exit code was {reader.ExitCode}. {dump}");
            Assert.True(session.HasExited, $"session did not report exit. {dump}");
            Assert.True(session.ExitCode == 0, $"session exit code was {session.ExitCode}. {dump}");
            Assert.True(text.Contains("hello"), $"child output never contained 'hello'. {dump}");
        }
        finally
        {
            reader.Dispose();
            session.Dispose();
        }
    }

    [Trait("Suite", "PtyInteractive")]
    [PlatformFact(TestOperatingSystem.Windows)]
    public void ConPtyForwardsInputToLiveChild()
    {
        var logger = NullLogger.Instance;
        var host = PtyHostFactory.Create(logger);
        var session = host.Spawn(new PtySpawnRequest
        {
            Command = "cmd.exe",
            Args = new[] { "/k" },
            Cols = 80,
            Rows = 24,
        });

        var ring = new PtyRingBuffer(1 << 20);
        var signal = new PtyRingSignal();
        var reader = new PtySessionReader(session, ring, signal, logger);
        reader.Start();

        var cursor = new PtyClientCursor();
        var sink = new RecordingSink();
        var delivery = new PtyDelivery(session.SessionId, ring, cursor, sink);
        try
        {
            session.Write(Encoding.UTF8.GetBytes("echo marker123\r\n"));

            var sw = Stopwatch.StartNew();
            string text = string.Empty;
            while (sw.Elapsed.TotalSeconds < 10)
            {
                delivery.PumpAvailable();
                text = Encoding.UTF8.GetString(sink.Delivered);
                if (text.Contains("marker123"))
                    break;
                Thread.Sleep(20);
            }

            delivery.PumpAvailable();
            byte[] raw = sink.Delivered;
            text = Encoding.UTF8.GetString(raw);
            Assert.True(text.Contains("marker123"), $"written input never surfaced as child output. {DescribeCapture(raw, text)}");
        }
        finally
        {
            reader.Dispose();
            session.Dispose();
        }
    }

    [Trait("Suite", "PtyInteractive")]
    [PlatformFact(TestOperatingSystem.Windows)]
    public void ConPtyDisposeDoesNotHangForLiveProcess()
    {
        var logger = NullLogger.Instance;
        var host = PtyHostFactory.Create(logger);
        var session = host.Spawn(new PtySpawnRequest
        {
            Command = "cmd.exe",
            Args = new[] { "/k", "prompt $G" },
            Cols = 80,
            Rows = 24,
        });

        var teardown = new Thread(() =>
        {
            session.Resize(120, 30);
            session.Dispose();
        })
        {
            IsBackground = true,
        };
        teardown.Start();
        var completedInTime = teardown.Join(TimeSpan.FromSeconds(10));
        if (!completedInTime)
        {
            try
            {
                session.Kill();
            }
            catch (ObjectDisposedException)
            {
            }
            Assert.True(
                teardown.Join(TimeSpan.FromSeconds(5)),
                "ConPTY dispose remained hung after the child was killed.");
        }
        Assert.True(completedInTime, "ConPTY dispose hung for a live process.");
    }

    [PlatformFact(TestOperatingSystem.Windows)]
    public void DisposeTransfersDelayedWatcherResourcesWithoutUseAfterClose()
    {
        using var watcherEntered = new ManualResetEventSlim();
        using var releaseWatcher = new ManualResetEventSlim();
        using var exitSignalSet = new ManualResetEventSlim();
        using var watcherResourcesClosed = new ManualResetEventSlim();
        var closedHandles = new List<IntPtr>();
        object closedHandlesLock = new();
        int watcherReleaseTimedOut = 0;
        var hooks = new WindowsPtySessionTestHooks
        {
            WaitForExit = () =>
            {
                watcherEntered.Set();
                if (!releaseWatcher.Wait(TimeSpan.FromSeconds(5)))
                {
                    Volatile.Write(ref watcherReleaseTimedOut, 1);
                    return -1;
                }
                return 29;
            },
            TerminateProcess = () => { },
            CloseHandle = handle =>
            {
                lock (closedHandlesLock)
                    closedHandles.Add(handle);
            },
            ExitSignalSet = exitSignalSet.Set,
            WatcherResourcesClosed = watcherResourcesClosed.Set,
            DisposeTimeout = TimeSpan.FromMilliseconds(100),
        };
        var session = CreateTestSession(hooks);
        try
        {
            Assert.True(
                watcherEntered.Wait(TimeSpan.FromSeconds(5)),
                "Exit watcher did not enter its delayed wait within 5 seconds.");

            var stopwatch = Stopwatch.StartNew();
            var failure = Assert.Throws<TimeoutException>(session.Dispose);
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(2),
                $"Dispose exceeded its bounded deadline: {stopwatch.Elapsed}.");
            Assert.Contains("exit watcher", failure.Message, StringComparison.OrdinalIgnoreCase);
            lock (closedHandlesLock)
                Assert.Empty(closedHandles);
            Assert.False(watcherResourcesClosed.IsSet);

            releaseWatcher.Set();
            Assert.True(
                exitSignalSet.Wait(TimeSpan.FromSeconds(5)),
                "Watcher did not safely set the retained exit event within 5 seconds.");
            Assert.True(
                watcherResourcesClosed.Wait(TimeSpan.FromSeconds(5)),
                "Watcher did not close its transferred resources within 5 seconds.");
            lock (closedHandlesLock)
            {
                Assert.Equal(
                    new[] { new IntPtr(202), new IntPtr(101) },
                    closedHandles);
            }
            Assert.Equal(0, Volatile.Read(ref watcherReleaseTimedOut));
            Assert.True(session.HasExited);
            Assert.Equal(29, session.ExitCode);
        }
        finally
        {
            releaseWatcher.Set();
            session.Dispose();
        }
    }

    [PlatformFact(TestOperatingSystem.Windows)]
    public void DisposeAfterWatcherExitClosesResourcesExactlyOnce()
    {
        using var exitSignalSet = new ManualResetEventSlim();
        int resourcesClosed = 0;
        var closedHandles = new List<IntPtr>();
        var outputRead = new SafeFileHandle(IntPtr.Zero, ownsHandle: false);
        var inputWrite = new SafeFileHandle(IntPtr.Zero, ownsHandle: false);
        var hooks = new WindowsPtySessionTestHooks
        {
            WaitForExit = () => 17,
            TerminateProcess = () => { },
            CloseHandle = closedHandles.Add,
            ExitSignalSet = exitSignalSet.Set,
            WatcherResourcesClosed = () => Interlocked.Increment(ref resourcesClosed),
            DisposeTimeout = TimeSpan.FromSeconds(1),
        };
        var session = CreateTestSession(hooks, outputRead, inputWrite);

        Assert.True(
            exitSignalSet.Wait(TimeSpan.FromSeconds(5)),
            "Exit watcher did not signal completion within 5 seconds.");
        session.Dispose();
        session.Dispose();

        Assert.Equal(1, Volatile.Read(ref resourcesClosed));
        Assert.Equal(new[] { new IntPtr(202), new IntPtr(101) }, closedHandles);
        Assert.Equal(17, session.WaitForExit());
        Assert.True(outputRead.IsClosed);
        Assert.True(inputWrite.IsClosed);
    }

    private static WindowsPtySession CreateTestSession(
        WindowsPtySessionTestHooks hooks,
        SafeFileHandle? outputRead = null,
        SafeFileHandle? inputWrite = null)
    {
        return new WindowsPtySession(
            sessionId: 77,
            pseudoConsole: IntPtr.Zero,
            outputRead ?? new SafeFileHandle(IntPtr.Zero, ownsHandle: false),
            inputWrite ?? new SafeFileHandle(IntPtr.Zero, ownsHandle: false),
            processHandle: new IntPtr(101),
            threadHandle: new IntPtr(202),
            processId: 303,
            logger: NullLogger.Instance,
            testHooks: hooks);
    }

    private static string DescribeCapture(byte[] raw, string text)
    {
        var escaped = new StringBuilder(text.Length + 16);
        foreach (char c in text)
        {
            if (c == '\\')
                escaped.Append("\\\\");
            else if (c == '\x1b')
                escaped.Append("\\e");
            else if (c == '\r')
                escaped.Append("\\r");
            else if (c == '\n')
                escaped.Append("\\n");
            else if (c < 0x20 || c == 0x7f)
                escaped.Append("\\x").Append(((int)c).ToString("x2"));
            else
                escaped.Append(c);
        }
        return $"captured {raw.Length} bytes: \"{escaped}\"";
    }
}
