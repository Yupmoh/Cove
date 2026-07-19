using System.Buffers.Binary;
using System.Text.Json;
using Cove.Platform.Pty.Unix;
using Cove.Protocol;

namespace Cove.Engine.Restart;

public static class HandoffWire
{
    private const int MaxRecordJsonBytes = 1 << 26;
    private const int RingChunkBytes = 1 << 18;

    public static void WriteRecord(int socketFd, HandoffNookRecord record, int transferFd, ReadOnlySpan<byte> ring)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(record, CoveJsonContext.Default.HandoffNookRecord);
        Span<byte> header = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, json.Length);
        var framed = new byte[4 + json.Length];
        header.CopyTo(framed);
        json.CopyTo(framed.AsSpan(4));
        UnixFdChannel.Send(socketFd, framed, transferFd);
        for (var offset = 0; offset < ring.Length; offset += RingChunkBytes)
            UnixFdChannel.Send(socketFd, ring.Slice(offset, Math.Min(RingChunkBytes, ring.Length - offset)));
    }

    public static (HandoffNookRecord Record, int Fd, byte[] Ring)? ReadRecord(int socketFd)
    {
        var fd = -1;
        Span<byte> header = stackalloc byte[4];
        if (!ReadExact(socketFd, header, ref fd))
            return null;
        var jsonLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (jsonLength <= 0 || jsonLength > MaxRecordJsonBytes)
            throw new InvalidOperationException($"handoff record header rejected (length {jsonLength}).");
        var json = new byte[jsonLength];
        if (!ReadExact(socketFd, json, ref fd))
            return null;
        var record = JsonSerializer.Deserialize(json, CoveJsonContext.Default.HandoffNookRecord)
            ?? throw new InvalidOperationException("handoff record decoded to null.");
        if (string.IsNullOrEmpty(record.NookToken))
            throw new InvalidOperationException(
                "handoff record nook credential is missing.");
        if (record.RingLength < 0)
            throw new InvalidOperationException($"handoff record ring length rejected ({record.RingLength}).");
        var ring = new byte[record.RingLength];
        if (record.RingLength > 0 && !ReadExact(socketFd, ring, ref fd))
            return null;
        return (record, fd, ring);
    }

    private static bool ReadExact(int socketFd, Span<byte> destination, ref int fd)
    {
        var filled = 0;
        while (filled < destination.Length)
        {
            var n = UnixFdChannel.Receive(socketFd, destination[filled..], out var receivedFd);
            if (receivedFd >= 0)
            {
                if (fd >= 0)
                    UnixFdChannel.CloseFd(fd);
                fd = receivedFd;
            }
            if (n == 0)
                return false;
            filled += n;
        }
        return true;
    }
}
