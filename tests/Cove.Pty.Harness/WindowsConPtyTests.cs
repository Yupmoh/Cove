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

    [Trait("Category", "PtyInteractive")]
    [Fact]
    public void ConPtyForwardsInputToLiveChild()
    {
        if (!OperatingSystem.IsWindows())
            return;

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
