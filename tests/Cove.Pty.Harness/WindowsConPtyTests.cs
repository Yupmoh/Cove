using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Pty.Harness;

public sealed class WindowsConPtyTests
{
    [Trait("Category", "PtyInteractive")]
    [Fact]
    public void ConPtySpawnEchoesOutputAndExitsWithCodeZero()
    {
        if (!OperatingSystem.IsWindows())
            return;

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
            Assert.True(reader.HasCompleted);
            Assert.Equal(0, reader.ExitCode);
            Assert.True(session.HasExited);
            Assert.Equal(0, session.ExitCode);

            var cursor = new PtyClientCursor();
            var sink = new RecordingSink();
            var delivery = new PtyDelivery(session.SessionId, ring, cursor, sink);
            delivery.PumpAvailable();

            string text = Encoding.UTF8.GetString(sink.Delivered);
            Assert.Contains("hello", text);
        }
        finally
        {
            reader.Dispose();
            session.Dispose();
        }
    }

    [Trait("Category", "PtyInteractive")]
    [Fact]
    public void ConPtyDisposeDoesNotHangForLiveProcess()
    {
        if (!OperatingSystem.IsWindows())
            return;

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
        Assert.True(teardown.Join(TimeSpan.FromSeconds(10)), "ConPTY dispose hung for a live process.");
    }
}
