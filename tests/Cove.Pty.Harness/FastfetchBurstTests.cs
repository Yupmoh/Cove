using Cove.Pty.Harness;
using Cove.Testing;
using Xunit;

namespace Cove.Pty.Harness.Tests;

public sealed class FastfetchBurstTests
{
    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait("Suite", "PtyDrain")]
    public void FastfetchBurst_NeverWedges_DrainsAllBytes()
    {
        const long total = 16L * 1024 * 1024;
        var (ring, reader, session) = PtyHarnessFixture.StartGen(65536, total);
        try
        {
            PtyHarnessFixture.WaitDrained(reader, 10);
            Assert.True(reader.HasCompleted);
            Assert.Equal(0, reader.ExitCode);
            Assert.Equal(total, ring.Head);
        }
        finally
        {
            session.Kill();
            reader.Dispose();
            session.Dispose();
        }
    }
}
