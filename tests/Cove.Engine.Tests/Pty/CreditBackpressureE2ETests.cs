using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Cove.Engine.Pty;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests.Pty;

public sealed class CreditBackpressureE2ETests
{
    private static byte[] Pattern(long from, int len)
    {
        var b = new byte[len];
        for (int i = 0; i < len; i++)
            b[i] = (byte)((from + i) % 256);
        return b;
    }

    private static (TcpClient server, TcpClient client) ConnectLoopback()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        var server = listener.AcceptTcpClient();
        listener.Stop();
        const int buf = 8 * 1024 * 1024;
        server.SendBufferSize = buf;
        server.ReceiveBufferSize = buf;
        client.SendBufferSize = buf;
        client.ReceiveBufferSize = buf;
        return (server, client);
    }

    private static (FrameType type, ulong streamId, byte[] payload) ReadFrame(Stream s)
    {
        var hdr = new byte[ProtocolConstants.HeaderSize];
        s.ReadExactly(hdr, 0, hdr.Length);
        Assert.True(FrameHeader.TryRead(hdr, out var h, out var err), $"bad header: {err}");
        var payload = new byte[h.Length];
        if (h.Length > 0)
            s.ReadExactly(payload, 0, payload.Length);
        return (h.Type, h.StreamId, payload);
    }

    private static void WriteCredit(Stream s, ulong streamId, ulong ackOffset)
    {
        var frame = new byte[ProtocolConstants.HeaderSize + 8];
        FrameHeader.Write(frame, new FrameHeader(FrameType.Credit, streamId, 1, 8));
        BinaryPrimitives.WriteUInt64LittleEndian(frame.AsSpan(ProtocolConstants.HeaderSize, 8), ackOffset);
        s.Write(frame, 0, frame.Length);
        s.Flush();
    }

    private static long ReadCredit(Stream s)
    {
        var (type, _, payload) = ReadFrame(s);
        Assert.Equal(FrameType.Credit, type);
        return (long)BinaryPrimitives.ReadUInt64LittleEndian(payload.AsSpan(0, 8));
    }

    private static void ReadStreamData(Stream s, MemoryStream into, ref long expected, int wantRaw)
    {
        int got = 0;
        while (got < wantRaw)
        {
            var (type, sid, payload) = ReadFrame(s);
            Assert.Equal(FrameType.StreamData, type);
            Assert.Equal(1ul, sid);
            ulong off = BinaryPrimitives.ReadUInt64LittleEndian(payload.AsSpan(0, 8));
            Assert.Equal((ulong)expected, off);
            int raw = payload.Length - 8;
            into.Write(payload, 8, raw);
            expected += raw;
            got += raw;
        }
        Assert.Equal(wantRaw, got);
    }

    [Fact]
    public void EndToEnd_WindowBackpressure_ByteIdentical_OverWire()
    {
        var (server, client) = ConnectLoopback();
        try
        {
            Stream ss = server.GetStream();
            Stream cs = client.GetStream();

            var ring = new PtyRingBuffer(1 << 20);
            var sink = new SocketByteStreamSink(ss);
            var sender = new PtyStreamSender(1, 1, ring, 0, sink);

            var received = new MemoryStream();
            long expected = 0;

            ring.Append(Pattern(0, 512 * 1024));
            sender.PumpAvailable();
            Assert.Equal(262144L, sender.SentOffset);
            ReadStreamData(cs, received, ref expected, 262144);

            WriteCredit(cs, 1, 131072);
            sender.OnCredit((ulong)ReadCredit(ss));
            Assert.Equal(393216L, sender.SentOffset);
            ReadStreamData(cs, received, ref expected, 131072);

            ring.Append(Pattern(512 * 1024, 64 * 1024));
            sender.PumpAvailable();
            Assert.Equal(393216L, sender.SentOffset);

            sender.MarkChildExited(0);
            WriteCredit(cs, 1, (ulong)sender.SentOffset);
            sender.OnCredit((ulong)ReadCredit(ss));
            Assert.True(sender.Ended);
            Assert.Equal(589824L, sender.SentOffset);
            ReadStreamData(cs, received, ref expected, 196608);

            var (etype, esid, epayload) = ReadFrame(cs);
            Assert.Equal(FrameType.StreamEnd, etype);
            Assert.Equal(1ul, esid);
            Assert.Equal(589824ul, BinaryPrimitives.ReadUInt64LittleEndian(epayload.AsSpan(0, 8)));
            Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(epayload.AsSpan(8, 4)));

            var all = received.ToArray();
            Assert.Equal(589824, all.Length);
            for (int i = 0; i < all.Length; i++)
                Assert.Equal((byte)(i % 256), all[i]);
        }
        finally
        {
            client.Dispose();
            server.Dispose();
        }
    }

    [Fact]
    public void EndToEnd_ForcedUnderrun_ExactlyOneResync_OverWire()
    {
        var (server, client) = ConnectLoopback();
        try
        {
            Stream ss = server.GetStream();
            Stream cs = client.GetStream();

            var ring = new PtyRingBuffer(65536);
            ring.Append(Pattern(0, 262144));
            var sink = new SocketByteStreamSink(ss);
            var sender = new PtyStreamSender(1, 1, ring, 0, sink);

            sender.PumpAvailable();
            Assert.Equal(262144L, sender.SentOffset);

            var (t0, sid0, p0) = ReadFrame(cs);
            Assert.Equal(FrameType.Resync, t0);
            Assert.Equal(1ul, sid0);
            Assert.Equal(196608ul, BinaryPrimitives.ReadUInt64LittleEndian(p0.AsSpan(0, 8)));

            var (t1, sid1, p1) = ReadFrame(cs);
            Assert.Equal(FrameType.StreamData, t1);
            Assert.Equal(1ul, sid1);
            Assert.Equal(196608ul, BinaryPrimitives.ReadUInt64LittleEndian(p1.AsSpan(0, 8)));
            int raw = p1.Length - 8;
            Assert.Equal(65536, raw);
            for (int i = 0; i < raw; i++)
                Assert.Equal((byte)((196608 + i) % 256), p1[8 + i]);

            Assert.Equal(262144L, sender.SentOffset);
        }
        finally
        {
            client.Dispose();
            server.Dispose();
        }
    }
}
