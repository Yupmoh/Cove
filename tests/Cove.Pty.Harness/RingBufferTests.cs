using System;
using Cove.Engine.Pty;
using Xunit;

namespace Cove.Pty.Harness.Tests;

public sealed class RingBufferTests
{
    private static byte[] Pattern(long from, int len)
    {
        var a = new byte[len];
        for (int i = 0; i < len; i++)
            a[i] = (byte)((from + i) % 256);
        return a;
    }

    [Fact]
    [Trait("Suite", "PtyRing")]
    public void Replay_FromAnyOffset_IsByteExact()
    {
        var ring = new PtyRingBuffer(4096);
        const int n = 2048;
        ring.Append(Pattern(0, n));
        foreach (int k in new[] { 0, 100, 2047 })
        {
            var dest = new byte[n];
            var r = ring.ReadInto(k, dest);
            Assert.False(r.Underrun);
            Assert.Equal(n - k, r.BytesCopied);
            Assert.Equal((long)n, r.NextOffset);
            for (int i = 0; i < r.BytesCopied; i++)
                Assert.Equal((byte)((k + i) % 256), dest[i]);
        }
    }

    [Fact]
    [Trait("Suite", "PtyRing")]
    public void Eviction_OldestOverwritten()
    {
        const int c = 4096;
        var ring = new PtyRingBuffer(c);
        ring.Append(Pattern(0, c));
        ring.Append(Pattern(c, c));
        Assert.Equal((long)c, ring.Tail);
        Assert.Equal((long)(2 * c), ring.Head);

        var dest = new byte[c];
        var under = ring.ReadInto(0, dest);
        Assert.True(under.Underrun);
        Assert.Equal((long)c, under.NextOffset);

        var ok = ring.ReadInto(c, dest);
        Assert.False(ok.Underrun);
        Assert.Equal(c, ok.BytesCopied);
        for (int i = 0; i < c; i++)
            Assert.Equal((byte)((c + i) % 256), dest[i]);
    }

    [Fact]
    [Trait("Suite", "PtyRing")]
    public void IndependentCursors_DoNotInterfere()
    {
        var ring = new PtyRingBuffer(4096);
        ring.Append(Pattern(0, 4096));
        const long a = 100;
        const long b = 2000;
        var da = new byte[64];
        var db = new byte[64];
        var ra = ring.ReadInto(a, da);
        var rb = ring.ReadInto(b, db);
        Assert.Equal(64, ra.BytesCopied);
        Assert.Equal(64, rb.BytesCopied);
        Assert.Equal(a + 64, ra.NextOffset);
        Assert.Equal(b + 64, rb.NextOffset);
        for (int i = 0; i < 64; i++)
        {
            Assert.Equal((byte)((a + i) % 256), da[i]);
            Assert.Equal((byte)((b + i) % 256), db[i]);
        }
    }

    [Fact]
    [Trait("Suite", "PtyRing")]
    public void Constructor_RejectsInvalidCapacity()
    {
        Assert.Throws<ArgumentException>(() => new PtyRingBuffer(4095));
        Assert.Throws<ArgumentException>(() => new PtyRingBuffer(6000));
        Assert.Throws<ArgumentException>(() => new PtyRingBuffer(2048));
    }
}
