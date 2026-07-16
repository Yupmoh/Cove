using System.Diagnostics;
using System.Text;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Pty.Harness;

public sealed class UnixEnvironmentReplaceTests
{
    [Fact]
    public void ProvidedEnvironment_ReplacesHostEnvironmentEntirely()
    {
        if (OperatingSystem.IsWindows())
            return;
        Environment.SetEnvironmentVariable("COVE_TEST_HOST_LEAK", "1");
        try
        {
            var logger = NullLogger.Instance;
            var host = PtyHostFactory.Create(logger);
            var session = host.Spawn(new PtySpawnRequest
            {
                Command = "/usr/bin/env",
                Args = Array.Empty<string>(),
                Environment = new Dictionary<string, string>
                {
                    ["PATH"] = "/usr/bin:/bin",
                    ["HOME"] = Environment.GetEnvironmentVariable("HOME") ?? "/tmp",
                    ["COVE_TEST_MARKER"] = "present",
                },
                Cols = 80,
                Rows = 24,
            });
            var ring = new PtyRingBuffer(1 << 20);
            var signal = new PtyRingSignal();
            var reader = new PtySessionReader(session, ring, signal, logger);
            reader.Start();
            try
            {
                var sw = Stopwatch.StartNew();
                while (!reader.HasCompleted && sw.Elapsed.TotalSeconds < 15)
                    Thread.Sleep(5);
                Assert.True(reader.HasCompleted);

                var cursor = new PtyClientCursor();
                var sink = new RecordingSink();
                var delivery = new PtyDelivery(session.SessionId, ring, cursor, sink);
                delivery.PumpAvailable();

                string text = Encoding.UTF8.GetString(sink.Delivered);
                Assert.Contains("COVE_TEST_MARKER=present", text);
                Assert.DoesNotContain("COVE_TEST_HOST_LEAK", text);
            }
            finally
            {
                reader.Dispose();
                session.Dispose();
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("COVE_TEST_HOST_LEAK", null);
        }
    }
}
