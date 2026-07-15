using System;
using System.Text;
using Cove.Engine.Pty;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests.Pty;

public sealed class PtyStreamSenderTests
{
    private static byte[] Pattern(long from, int len)
    {
        var b = new byte[len];
        for (int i = 0; i < len; i++)
            b[i] = (byte)((from + i) % 256);
        return b;
    }

    [Fact]
    public void DefaultRingRetainsEightMiB()
    {
        var ring = new PtyRingBuffer();
        Assert.Equal(8 * 1024 * 1024, ring.Capacity);
    }

    [Fact]
    public void WindowPausesAtFlowWindow()
    {
        var ring = new PtyRingBuffer(1 << 20);
        ring.Append(Pattern(0, 512 * 1024));
        var sink = new RecordingFrameSink();
        var sender = new PtyStreamSender(1, 1, ring, 0, sink);

        sender.PumpAvailable();

        Assert.Equal(ProtocolConstants.FlowWindow, sink.AllData().Length);
        Assert.Equal(ProtocolConstants.FlowWindow, sender.SentOffset);
        Assert.Equal(0, sink.ResyncCount);
        Assert.Equal(4, sink.Data.Count);
        Assert.Equal(0ul, sink.Data[0].Offset);
        Assert.Equal(65536ul, sink.Data[1].Offset);
        Assert.Equal(131072ul, sink.Data[2].Offset);
        Assert.Equal(196608ul, sink.Data[3].Offset);
        var all = sink.AllData();
        for (int i = 0; i < all.Length; i++)
            Assert.Equal((byte)(i % 256), all[i]);
    }

    [Fact]
    public void CreditResumesSender()
    {
        var ring = new PtyRingBuffer(1 << 20);
        ring.Append(Pattern(0, 512 * 1024));
        var sink = new RecordingFrameSink();
        var sender = new PtyStreamSender(1, 1, ring, 0, sink);
        sender.PumpAvailable();
        Assert.Equal(262144L, sender.SentOffset);

        sender.OnCredit(131072);

        Assert.Equal(393216L, sender.SentOffset);
        Assert.Equal(393216, sink.AllData().Length);
        Assert.Empty(sink.Errors);
    }

    [Fact]
    public void InvalidCreditAboveSentTearsDownStream()
    {
        var ring = new PtyRingBuffer(1 << 20);
        ring.Append(Pattern(0, 128 * 1024));
        var sink = new RecordingFrameSink();
        var sender = new PtyStreamSender(1, 1, ring, 0, sink);
        sender.PumpAvailable();
        Assert.Equal(131072L, sender.SentOffset);

        sender.OnCredit(200000);

        Assert.True(sender.Faulted);
        Assert.Single(sink.Errors);
        Assert.Equal("invalid_credit", sink.Errors[0].Code);
    }

    [Fact]
    public void InvalidCreditDecreasingTearsDownStream()
    {
        var ring = new PtyRingBuffer(1 << 20);
        ring.Append(Pattern(0, 128 * 1024));
        var sink = new RecordingFrameSink();
        var sender = new PtyStreamSender(1, 1, ring, 0, sink);
        sender.PumpAvailable();

        sender.OnCredit(1000);
        sender.OnCredit(500);

        Assert.True(sender.Faulted);
        Assert.Single(sink.Errors);
        Assert.Equal("invalid_credit", sink.Errors[0].Code);
    }

    [Fact]
    public void ForcedUnderrunEmitsExactlyOneResync()
    {
        var ring = new PtyRingBuffer(65536);
        ring.Append(Pattern(0, 262144));
        var sink = new RecordingFrameSink();
        var sender = new PtyStreamSender(1, 1, ring, 0, sink, terminalModePreamble: () => "\x1b[?1049h");

        sender.PumpAvailable();

        Assert.Equal(1, sink.ResyncCount);
        Assert.Equal(196608ul, sink.Resyncs[0].NewBaseOffset);
        Assert.Equal(new byte[] { 27, 91, 63, 49, 48, 52, 57, 104 }, sink.Resyncs[0].TerminalModePreamble);
        Assert.Equal(262144L, sender.SentOffset);
        Assert.Single(sink.Data);
        Assert.Equal(196608ul, sink.Data[0].Offset);
        Assert.Equal(65536, sink.Data[0].Raw.Length);
        for (int i = 0; i < 65536; i++)
            Assert.Equal((byte)((196608 + i) % 256), sink.Data[0].Raw[i]);
    }

    [Fact]
    public void ForcedUnderrunResyncsFromLatestTerminalCheckpoint()
    {
        var ring = new PtyRingBuffer(65536);
        ring.Append(Pattern(0, 100000));
        var sink = new RecordingFrameSink();
        var checkpoint = Encoding.ASCII.GetBytes("STATE");
        var sender = new PtyStreamSender(
            1,
            1,
            ring,
            0,
            sink,
            terminalModePreamble: () => "\x1b[?1049h",
            terminalCheckpoint: () => new TerminalResyncSnapshot(50000, checkpoint, 132, 40, "\x1b[?1006h"));

        sender.PumpAvailable();

        var resync = Assert.Single(sink.Resyncs);
        Assert.Equal(50000ul, resync.NewBaseOffset);
        Assert.Equal(checkpoint, resync.TerminalCheckpoint);
        Assert.Equal(132, resync.CheckpointCols);
        Assert.Equal(40, resync.CheckpointRows);
        Assert.Equal(Encoding.ASCII.GetBytes("\x1b[?1006h"), resync.TerminalModePreamble);
        Assert.Equal(100000L, sender.SentOffset);
    }

    [Fact]
    public void NineMiBOverflowResyncsFromCheckpointInsteadOfTruncatedTail()
    {
        const int capacity = 8 * 1024 * 1024;
        const int checkpointOffset = 2 * 1024 * 1024;
        var ring = new PtyRingBuffer(capacity);
        ring.Append(Pattern(0, 9 * 1024 * 1024));
        var sink = new RecordingFrameSink();
        var checkpoint = Encoding.ASCII.GetBytes("NINE_MIB_STATE");
        var sender = new PtyStreamSender(
            1,
            1,
            ring,
            0,
            sink,
            terminalCheckpoint: () => new TerminalResyncSnapshot(checkpointOffset, checkpoint, 80, 24, ""));

        sender.PumpAvailable();

        var resync = Assert.Single(sink.Resyncs);
        Assert.Equal((ulong)checkpointOffset, resync.NewBaseOffset);
        Assert.Equal(checkpoint, resync.TerminalCheckpoint);
        Assert.Equal((long)checkpointOffset + ProtocolConstants.FlowWindow, sender.SentOffset);
    }

    [Fact]
    public void LosslessDeliveryByteIdentical()
    {
        var ring = new PtyRingBuffer(1 << 20);
        ring.Append(Pattern(0, 512 * 1024));
        var sink = new RecordingFrameSink();
        var sender = new PtyStreamSender(1, 1, ring, 0, sink);

        for (int guard = 0; guard < 1000 && sender.SentOffset < ring.Head; guard++)
        {
            sender.PumpAvailable();
            sender.OnCredit((ulong)sender.SentOffset);
        }

        Assert.Equal(0, sink.ResyncCount);
        Assert.Empty(sink.Errors);
        var all = sink.AllData();
        Assert.Equal(512 * 1024, all.Length);
        for (int i = 0; i < all.Length; i++)
            Assert.Equal((byte)(i % 256), all[i]);
    }

    [Fact]
    public void StreamEndAfterLastData()
    {
        var ring = new PtyRingBuffer(1 << 20);
        ring.Append(Pattern(0, 100000));
        var sink = new RecordingFrameSink();
        var sender = new PtyStreamSender(1, 1, ring, 0, sink);
        sender.MarkChildExited(0);

        for (int guard = 0; guard < 1000 && !sender.Ended; guard++)
        {
            sender.PumpAvailable();
            sender.OnCredit((ulong)sender.SentOffset);
        }

        Assert.True(sender.Ended);
        Assert.Single(sink.Ends);
        Assert.Equal(100000ul, sink.Ends[0].FinalOffset);
        Assert.Equal(0, sink.Ends[0].ExitCode);
        Assert.Equal(100000, sink.AllData().Length);
    }
}
