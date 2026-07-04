using System.Buffers.Binary;

namespace Cove.Protocol;

public readonly record struct FrameHeader(
    FrameType Type,
    ulong StreamId,
    uint Seq,
    uint Length)
{
    public static void Write(Span<byte> dst, in FrameHeader h)
    {
        if (dst.Length < ProtocolConstants.HeaderSize)
            throw new ArgumentException("header buffer too small", nameof(dst));
        ProtocolConstants.Magic.CopyTo(dst);
        dst[4] = ProtocolConstants.WireVersion;
        dst[5] = (byte)h.Type;
        dst[6] = 0;
        dst[7] = 0;
        BinaryPrimitives.WriteUInt64LittleEndian(dst.Slice(8, 8), h.StreamId);
        BinaryPrimitives.WriteUInt32LittleEndian(dst.Slice(16, 4), h.Seq);
        BinaryPrimitives.WriteUInt32LittleEndian(dst.Slice(20, 4), h.Length);
    }

    public static bool TryRead(ReadOnlySpan<byte> src, out FrameHeader h, out string? error)
    {
        h = default;
        error = null;
        if (src.Length < ProtocolConstants.HeaderSize) { error = "short_header"; return false; }
        if (!src.Slice(0, 4).SequenceEqual(ProtocolConstants.Magic)) { error = "malformed_frame"; return false; }
        if (src[4] != ProtocolConstants.WireVersion) { error = "unsupported_version"; return false; }
        byte type = src[5];
        if (type == 0 || type > (byte)FrameType.StreamEnd) { error = "unknown_frame_type"; return false; }
        if (src[6] != 0 || src[7] != 0) { error = "malformed_frame"; return false; }
        ulong streamId = BinaryPrimitives.ReadUInt64LittleEndian(src.Slice(8, 8));
        uint seq = BinaryPrimitives.ReadUInt32LittleEndian(src.Slice(16, 4));
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(src.Slice(20, 4));
        if (length > ProtocolConstants.MaxFramePayload) { error = "frame_too_large"; return false; }
        var t = (FrameType)type;
        bool isControl = t is FrameType.Request or FrameType.Response or FrameType.Event or FrameType.Error;
        if (isControl && streamId != 0) { error = "malformed_frame"; return false; }
        if (!isControl && streamId == 0) { error = "malformed_frame"; return false; }
        h = new FrameHeader(t, streamId, seq, length);
        return true;
    }
}
