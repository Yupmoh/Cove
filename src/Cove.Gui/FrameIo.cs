using System.Buffers.Binary;
using Cove.Protocol;

namespace Cove.Gui;

public readonly record struct RawFrame(FrameType Type, ulong StreamId, byte[] Payload);

public static class FrameIo
{
    public static async Task WriteAsync(Stream s, SemaphoreSlim writeGate, FrameType type, ulong streamId, uint seq, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        var buf = new byte[ProtocolConstants.HeaderSize + payload.Length];
        FrameHeader.Write(buf, new FrameHeader(type, streamId, seq, (uint)payload.Length));
        payload.Span.CopyTo(buf.AsSpan(ProtocolConstants.HeaderSize));
        await writeGate.WaitAsync(ct);
        try { await s.WriteAsync(buf, ct); await s.FlushAsync(ct); }
        finally { writeGate.Release(); }
    }

    public static async Task<RawFrame> ReadAsync(Stream s, CancellationToken ct)
    {
        var head = new byte[ProtocolConstants.HeaderSize];
        await s.ReadExactlyAsync(head, ct);
        if (!FrameHeader.TryRead(head, out var h, out var error))
            throw new InvalidDataException($"bad frame header: {error}");
        var payload = new byte[h.Length];
        if (h.Length > 0) await s.ReadExactlyAsync(payload, ct);
        return new RawFrame(h.Type, h.StreamId, payload);
    }

    public static byte[] U64(ulong v) { var b = new byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(b, v); return b; }
}
