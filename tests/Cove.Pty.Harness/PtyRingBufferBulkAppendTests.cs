using Cove.Engine.Pty;
using Xunit;

namespace Cove.Pty.Harness.Tests;

public sealed class PtyRingBufferBulkAppendTests
{
    [Fact]
    [Trait("Suite", "PtyRing")]
    public void BulkAppend_UnalignedHead_ReadBackIsExact()
    {
        var ring = new PtyRingBuffer(4096);
        ring.Append(new byte[] { 1, 2, 3 });

        var payload = new byte[8192];
        for (int i = 0; i < payload.Length; i++)
            payload[i] = (byte)(i % 251);
        ring.Append(payload);

        Assert.Equal(3 + 8192 - 4096, ring.Tail);

        var dest = new byte[4096];
        var result = ring.ReadInto(ring.Tail, dest);

        var expected = new byte[4096];
        Array.Copy(payload, 4096, expected, 0, 4096);

        Assert.Equal(expected, dest);
        Assert.Equal(4096, result.BytesCopied);
        Assert.Equal(3 + 8192, result.NextOffset);
        Assert.Equal(3 + 8192, ring.Head);
        Assert.False(result.Underrun);
    }

    [Fact]
    [Trait("Suite", "PtyRing")]
    public void BulkAppend_ExactCapacity_UnalignedHead_ReadBackIsExact()
    {
        var ring = new PtyRingBuffer(4096);
        ring.Append(new byte[] { 1, 2, 3, 4, 5 });

        var payload = new byte[4096];
        for (int i = 0; i < payload.Length; i++)
            payload[i] = (byte)(i % 251);
        ring.Append(payload);

        var dest = new byte[4096];
        var result = ring.ReadInto(ring.Tail, dest);

        Assert.Equal(payload, dest);
        Assert.Equal(4096, result.BytesCopied);
        Assert.False(result.Underrun);
    }

    [Fact]
    [Trait("Suite", "PtyRing")]
    public void BulkAppend_AlignedHead_StillExact()
    {
        var ring = new PtyRingBuffer(4096);

        var payload = new byte[12288];
        for (int i = 0; i < payload.Length; i++)
            payload[i] = (byte)(i % 251);
        ring.Append(payload);

        Assert.Equal(8192, ring.Tail);

        var dest = new byte[4096];
        var result = ring.ReadInto(8192, dest);

        var expected = new byte[4096];
        Array.Copy(payload, 8192, expected, 0, 4096);

        Assert.Equal(expected, dest);
        Assert.Equal(4096, result.BytesCopied);
        Assert.False(result.Underrun);
    }
}
