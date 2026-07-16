using System.Text.Json;
using Cove.Engine.Pty;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class HandoffManifestTests
{
    [Fact]
    public void NookRecord_RoundTripsThroughSourceGeneratedJson()
    {
        var record = new HandoffNookRecord(
            "nook-1", 4242, "/bin/zsh", new[] { "-i" }, "/repo", "/repo/sub", 120, 40,
            "build", "claude-code", "Claude Code", 987654321L, 4096,
            "sess-abc", "idle",
            new HandoffCheckpointDto("QUJD", 987650000L, 120, 40, 10000, "?25h"));

        var json = JsonSerializer.SerializeToUtf8Bytes(record, CoveJsonContext.Default.HandoffNookRecord);
        var back = JsonSerializer.Deserialize(json, CoveJsonContext.Default.HandoffNookRecord);

        Assert.Equal(record, back! with { Args = record.Args });
        Assert.Equal(record.Args, back.Args);
        Assert.Equal(987654321L, back.RingHead);
        Assert.Equal("QUJD", back.Checkpoint!.DataBase64);
    }

    [Fact]
    public void RingRestoreAt_PreservesAbsoluteOffsets()
    {
        var ring = new PtyRingBuffer(1 << 16);
        var tail = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        ring.RestoreAt(1_000_000, tail);

        Assert.Equal(1_000_000, ring.Head);
        Assert.Equal(1_000_000 - tail.Length, ring.Tail);
        Assert.True(ring.ContainsOffset(999_996));

        Span<byte> dest = stackalloc byte[4];
        var read = ring.ReadInto(1_000_000 - 4, dest);
        Assert.Equal(4, read.BytesCopied);
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, dest.ToArray());
    }

    [Fact]
    public void RingRestoreAt_ThenAppend_ContinuesTheOffsetSpace()
    {
        var ring = new PtyRingBuffer(1 << 16);
        ring.RestoreAt(500, new byte[] { 9, 9 });
        ring.Append(new byte[] { 7 });

        Assert.Equal(501, ring.Head);
        Span<byte> dest = stackalloc byte[3];
        var read = ring.ReadInto(498, dest);
        Assert.Equal(3, read.BytesCopied);
        Assert.Equal(new byte[] { 9, 9, 7 }, dest.ToArray());
    }
}
