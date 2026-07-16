using System.Text;
using Cove.Engine.Restart;
using Cove.Platform.Pty.Unix;
using Cove.Protocol;
using Xunit;

namespace Cove.Pty.Harness;

public sealed class HandoffWireTests
{
    private static HandoffNookRecord Record(string id, int ringLength) =>
        new(id, 4242, "/bin/zsh", new[] { "-i" }, "/repo", null, 80, 24, null, "omp", "omp", 1000 + ringLength, ringLength, "sess-1", "idle", null);

    [Fact]
    public async Task Records_RoundTripWithFdsAndRingPayloads()
    {
        if (OperatingSystem.IsWindows()) return;
        var (a, b) = UnixFdChannel.CreateSocketPair();
        var (fdIn, fdOut) = UnixFdChannel.CreateSocketPair();
        try
        {
            var smallRing = Encoding.UTF8.GetBytes("tail");
            var bigRing = new byte[1 << 20];
            new Random(7).NextBytes(bigRing);

            var writer = Task.Run(() =>
            {
                HandoffWire.WriteRecord(a, Record("nook-small", smallRing.Length), fdIn, smallRing);
                HandoffWire.WriteRecord(a, Record("nook-big", bigRing.Length), -1, bigRing);
            });

            var first = HandoffWire.ReadRecord(b);
            Assert.NotNull(first);
            Assert.Equal("nook-small", first!.Value.Record.NookId);
            Assert.Equal(smallRing, first.Value.Ring);
            Assert.True(first.Value.Fd >= 0);

            UnixFdChannel.Write(first.Value.Fd, Encoding.UTF8.GetBytes("fd-alive"));
            Span<byte> probe = stackalloc byte[16];
            var n = UnixFdChannel.Read(fdOut, probe);
            Assert.Equal("fd-alive", Encoding.UTF8.GetString(probe[..n]));
            UnixFdChannel.CloseFd(first.Value.Fd);

            var second = HandoffWire.ReadRecord(b);
            Assert.NotNull(second);
            Assert.Equal("nook-big", second!.Value.Record.NookId);
            Assert.Equal(bigRing, second.Value.Ring);
            Assert.Equal(-1, second.Value.Fd);

            await writer.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            UnixFdChannel.CloseFd(a);
            UnixFdChannel.CloseFd(b);
            UnixFdChannel.CloseFd(fdIn);
            UnixFdChannel.CloseFd(fdOut);
        }
    }

    [Fact]
    public void ReadRecord_OnClosedPeer_ReturnsNull()
    {
        if (OperatingSystem.IsWindows()) return;
        var (a, b) = UnixFdChannel.CreateSocketPair();
        UnixFdChannel.CloseFd(a);
        try
        {
            Assert.Null(HandoffWire.ReadRecord(b));
        }
        finally
        {
            UnixFdChannel.CloseFd(b);
        }
    }
}
