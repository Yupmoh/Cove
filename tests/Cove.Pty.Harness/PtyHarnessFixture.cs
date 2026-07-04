using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Cove.Platform.Pty.Unix;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cove.Pty.Harness;

public static class PtyHarnessFixture
{
    public static (PtyRingBuffer ring, PtySessionReader reader, IPtySession session) StartGen(int ringCapacity, long total)
    {
        var logger = NullLogger.Instance;
        var host = new UnixPtyHost(logger);
        string gen = Path.Combine(AppContext.BaseDirectory, "covptygen");
        var session = host.Spawn(new PtySpawnRequest
        {
            Command = gen,
            Args = new[] { total.ToString() },
        });
        var ring = new PtyRingBuffer(ringCapacity);
        var signal = new PtyRingSignal();
        var reader = new PtySessionReader(session, ring, signal, logger);
        reader.Start();
        return (ring, reader, session);
    }

    public static void WaitDrained(PtySessionReader reader, int timeoutSeconds = 10)
    {
        var sw = Stopwatch.StartNew();
        while (!reader.HasCompleted && sw.Elapsed.TotalSeconds < timeoutSeconds)
            Thread.Sleep(2);
        if (!reader.HasCompleted)
            throw new TimeoutException("drain did not complete: child wedged.");
    }
}
