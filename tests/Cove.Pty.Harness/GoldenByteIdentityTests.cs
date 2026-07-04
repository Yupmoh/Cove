using System;
using System.IO;
using System.Security.Cryptography;
using Cove.Engine.Pty;
using Cove.Pty.Harness;
using Xunit;

namespace Cove.Pty.Harness.Tests;

public sealed class GoldenByteIdentityTests
{
    [Fact]
    [Trait("Category", "PtyGolden")]
    public void Delivery_IsByteIdenticalToGolden()
    {
        const long total = 4L * 1024 * 1024;
        var (ring, reader, session) = PtyHarnessFixture.StartGen(8 * 1024 * 1024, total);
        try
        {
            PtyHarnessFixture.WaitDrained(reader, 10);
            Assert.Equal(total, ring.Head);
            Assert.Equal(0, reader.ExitCode);

            var cursor = new PtyClientCursor();
            var sink = new RecordingSink();
            var delivery = new PtyDelivery(session.SessionId, ring, cursor, sink);
            delivery.PumpAvailable();

            Assert.Equal(0, sink.ResyncCount);
            byte[] delivered = sink.Delivered;
            Assert.Equal((int)total, delivered.Length);
            for (int i = 0; i < delivered.Length; i++)
                Assert.Equal((byte)(i % 256), delivered[i]);

            var pattern = new byte[(int)total];
            for (int i = 0; i < pattern.Length; i++)
                pattern[i] = (byte)(i % 256);

            string golden = File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "golden", "covptygen-4mib.sha256")).Trim();
            string patternHash = Convert.ToHexString(SHA256.HashData(pattern)).ToLowerInvariant();
            string deliveredHash = Convert.ToHexString(SHA256.HashData(delivered)).ToLowerInvariant();
            Assert.Equal(golden, patternHash);
            Assert.Equal(golden, deliveredHash);
        }
        finally
        {
            session.Kill();
            reader.Dispose();
            session.Dispose();
        }
    }
}
