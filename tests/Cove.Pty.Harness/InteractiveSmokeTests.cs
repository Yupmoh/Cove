using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Pty.Harness;

public sealed class InteractiveSmokeTests
{
    [Trait("Category", "PtyInteractive")]
    [Fact]
    public void RealInteractiveShellEchoesMarkerAndExitsClean()
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
            session.Resize(100, 40);
            session.Write(Encoding.UTF8.GetBytes("printf 'COVE_SMOKE_%s\\n' OK\n"));
            Thread.Sleep(500);
            session.Write(Encoding.UTF8.GetBytes("exit\n"));

            var sw = Stopwatch.StartNew();
            while (!reader.HasCompleted && sw.Elapsed.TotalSeconds < 15)
                Thread.Sleep(5);
            Assert.True(reader.HasCompleted);

            var cursor = new PtyClientCursor();
            var sink = new RecordingSink();
            var delivery = new PtyDelivery(session.SessionId, ring, cursor, sink);
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
