using System.Buffers.Binary;
using Cove.Protocol;

namespace Cove.Tui.Attach;

public static class AttachFrameDecode
{
    public static ulong StreamDataOffset(ReadOnlySpan<byte> payload)
    {
        return BinaryPrimitives.ReadUInt64LittleEndian(payload.Slice(0, 8));
    }

    public static ReadOnlyMemory<byte> StreamDataRaw(ReadOnlyMemory<byte> payload)
    {
        return payload.Slice(8);
    }

    public static ulong ResyncOffset(ReadOnlySpan<byte> payload)
    {
        return BinaryPrimitives.ReadUInt64LittleEndian(payload.Slice(0, 8));
    }

    public static (ulong FinalOffset, int ExitCode) StreamEndFields(ReadOnlySpan<byte> payload)
    {
        return (BinaryPrimitives.ReadUInt64LittleEndian(payload.Slice(0, 8)),
                BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(8, 4)));
    }

    public static ulong NextAckOffset(ulong currentOffset, ulong dataOffset, int dataLength)
    {
        var newEnd = dataOffset + (ulong)dataLength;
        return newEnd > currentOffset ? newEnd : currentOffset;
    }

    public static byte[] EncodeCredit(ulong ackOffset)
    {
        var payload = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(payload, ackOffset);
        return payload;
    }

    public static bool IsStreamFrame(FrameType type)
    {
        return type is FrameType.StreamData or FrameType.Resync or FrameType.StreamEnd;
    }

    public static bool BelongsToStream(ulong frameStreamId, ulong sessionStreamId, FrameType type)
    {
        if (!IsStreamFrame(type)) return false;
        return frameStreamId == sessionStreamId;
    }
}
