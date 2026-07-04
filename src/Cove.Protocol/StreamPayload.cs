using System.Buffers.Binary;

namespace Cove.Protocol;

public static class StreamPayload
{
    public static int WriteStreamData(Span<byte> dst, ulong offset, ReadOnlySpan<byte> raw)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(dst.Slice(0, 8), offset);
        raw.CopyTo(dst.Slice(8, raw.Length));
        return 8 + raw.Length;
    }

    public static ulong ReadStreamDataOffset(ReadOnlySpan<byte> payload) =>
        BinaryPrimitives.ReadUInt64LittleEndian(payload.Slice(0, 8));

    public static ReadOnlySpan<byte> ReadStreamDataRaw(ReadOnlySpan<byte> payload) =>
        payload.Slice(8);

    public static void WriteOffset(Span<byte> dst, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(dst.Slice(0, 8), value);

    public static ulong ReadOffset(ReadOnlySpan<byte> payload) =>
        BinaryPrimitives.ReadUInt64LittleEndian(payload.Slice(0, 8));

    public static int WriteStreamEnd(Span<byte> dst, ulong finalOffset, int exitCode)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(dst.Slice(0, 8), finalOffset);
        BinaryPrimitives.WriteInt32LittleEndian(dst.Slice(8, 4), exitCode);
        return 12;
    }

    public static (ulong FinalOffset, int ExitCode) ReadStreamEnd(ReadOnlySpan<byte> payload) =>
        (BinaryPrimitives.ReadUInt64LittleEndian(payload.Slice(0, 8)),
         BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(8, 4)));
}
