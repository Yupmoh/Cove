using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Cove.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Pty.Harness;

public sealed class InteractiveSmokeTests
{
    [Trait("Suite", "PtyInteractive")]
    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task RealInteractiveShellEchoesMarkerAndExitsClean()
    {
        string shell = System.IO.File.Exists("/bin/zsh") ? "/bin/zsh" : "/bin/bash";
        var logger = NullLogger.Instance;
        var host = PtyHostFactory.Create(logger);
        var session = host.Spawn(new PtySpawnRequest
        {
            Command = shell,
            Args = new[] { "-i" },
            Cols = 80,
            Rows = 24,
        });
        var ring = new PtyRingBuffer(1 << 20);
        var signal = new PtyRingSignal();
        var reader = new PtySessionReader(session, ring, signal, logger);
        reader.Start();
        try
        {
            var cursor = new PtyClientCursor();
            var sink = new RecordingSink();
            var delivery = new PtyDelivery(session.SessionId, ring, cursor, sink);
            session.Resize(100, 40);
            session.Write(Encoding.UTF8.GetBytes("printf 'COVE_SMOKE_%s\\n' OK\n"));
            await AsyncTest.EventuallyAsync(
                () =>
                {
                    delivery.PumpAvailable();
                    return sink.Contains("COVE_SMOKE_OK"u8);
                },
                TimeSpan.FromSeconds(10),
                "interactive shell never emitted the marker");
            session.Write(Encoding.UTF8.GetBytes("exit\n"));

            await AsyncTest.EventuallyAsync(
                () => reader.HasCompleted,
                TimeSpan.FromSeconds(15),
                "interactive shell reader never completed");
            delivery.PumpAvailable();

            string text = Encoding.UTF8.GetString(sink.Delivered);
            Assert.Contains("COVE_SMOKE_OK", text);
        }
        finally
        {
            reader.Dispose();
            session.Dispose();
        }
    }
}
