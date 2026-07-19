using Cove.Engine.Pty;
using Cove.Pty.Harness;
using Cove.Testing;
using Xunit;

namespace Cove.Pty.Harness.Tests;

public sealed class ThrottledConsumerTests
{
    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait("Suite", "PtyResync")]
    public void ThrottledConsumer_ExactlyOneResync()
    {
        const long total = 262144;
        var (ring, reader, session) = PtyHarnessFixture.StartGen(65536, total);
        try
        {
            PtyHarnessFixture.WaitDrained(reader, 10);
            Assert.Equal(total, ring.Head);
            Assert.Equal(196608L, ring.Tail);

            var cursor = new PtyClientCursor();
            var sink = new RecordingSink();
            var delivery = new PtyDelivery(session.SessionId, ring, cursor, sink);
            delivery.PumpAvailable();

            Assert.Equal(1, sink.ResyncCount);
            Assert.Equal(196608L, sink.LastResyncOffset);
            Assert.Equal(total, cursor.Offset);
            byte[] d = sink.Delivered;
            Assert.Equal(65536, d.Length);
            for (int i = 0; i < d.Length; i++)
                Assert.Equal((byte)((196608 + i) % 256), d[i]);
        }
        finally
        {
            session.Kill();
            reader.Dispose();
            session.Dispose();
        }
    }
}
