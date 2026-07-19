using System.Buffers.Binary;

namespace Cove.Protocol;

public readonly record struct StreamDataMessage(ulong Offset, ReadOnlyMemory<byte> Data);

public readonly record struct StreamResyncMessage(
    ulong BaseOffset,
    ReadOnlyMemory<byte> TerminalModePreamble,
    ReadOnlyMemory<byte> TerminalCheckpoint,
    int CheckpointCols,
    int CheckpointRows);

public readonly record struct StreamEndMessage(ulong FinalOffset, int ExitCode);

public static class StreamPayload
{
    public const int OffsetSize = sizeof(ulong);
    public const int StreamEndSize = sizeof(ulong) + sizeof(int);
    public const int ResyncHeaderSize = sizeof(ulong) + sizeof(int) + sizeof(int) + sizeof(int);

    public static int WriteStreamData(Span<byte> dst, ulong offset, ReadOnlySpan<byte> raw)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(dst.Slice(0, 8), offset);
        raw.CopyTo(dst.Slice(8));
        return 8 + raw.Length;
    }

    public static ulong ReadStreamDataOffset(ReadOnlySpan<byte> payload) =>
        BinaryPrimitives.ReadUInt64LittleEndian(payload.Slice(0, 8));

    public static ReadOnlySpan<byte> ReadStreamDataRaw(ReadOnlySpan<byte> payload) =>
        payload.Slice(8);

    public static StreamDataMessage ReadStreamData(ReadOnlyMemory<byte> payload)
    {
        if (payload.Length < OffsetSize)
            throw new InvalidDataException($"StreamData payload is too short: expected at least {OffsetSize} bytes, received {payload.Length}");
        return new StreamDataMessage(
            BinaryPrimitives.ReadUInt64LittleEndian(payload.Span),
            payload.Slice(OffsetSize));
    }

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

    public static StreamEndMessage ReadStreamEndMessage(ReadOnlyMemory<byte> payload)
    {
        if (payload.Length < StreamEndSize)
            throw new InvalidDataException($"StreamEnd payload is too short: expected at least {StreamEndSize} bytes, received {payload.Length}");
        return new StreamEndMessage(
            BinaryPrimitives.ReadUInt64LittleEndian(payload.Span),
            BinaryPrimitives.ReadInt32LittleEndian(payload.Span.Slice(OffsetSize)));
    }

    public static (ulong FinalOffset, int ExitCode) ReadStreamEnd(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < StreamEndSize)
            throw new InvalidDataException($"StreamEnd payload is too short: expected at least {StreamEndSize} bytes, received {payload.Length}");
        return (
            BinaryPrimitives.ReadUInt64LittleEndian(payload),
            BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(OffsetSize)));
    }

    public static int WriteResync(
        Span<byte> destination,
        ulong baseOffset,
        int checkpointCols,
        int checkpointRows,
        ReadOnlySpan<byte> terminalModePreamble,
        ReadOnlySpan<byte> terminalCheckpoint)
    {
        int length = ResyncHeaderSize + terminalModePreamble.Length + terminalCheckpoint.Length;
        if (destination.Length < length)
            throw new ArgumentException($"destination requires {length} bytes", nameof(destination));
        BinaryPrimitives.WriteUInt64LittleEndian(destination, baseOffset);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(OffsetSize), checkpointCols);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(OffsetSize + sizeof(int)), checkpointRows);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(OffsetSize + (2 * sizeof(int))), terminalModePreamble.Length);
        terminalModePreamble.CopyTo(destination.Slice(ResyncHeaderSize));
        terminalCheckpoint.CopyTo(destination.Slice(ResyncHeaderSize + terminalModePreamble.Length));
        return length;
    }

    public static StreamResyncMessage ReadResync(ReadOnlyMemory<byte> payload)
    {
        if (payload.Length < OffsetSize)
            throw new InvalidDataException($"Resync payload is too short: expected at least {OffsetSize} bytes, received {payload.Length}");
        ulong baseOffset = BinaryPrimitives.ReadUInt64LittleEndian(payload.Span);
        if (payload.Length < ResyncHeaderSize)
        {
            return new StreamResyncMessage(
                baseOffset,
                payload.Slice(OffsetSize),
                ReadOnlyMemory<byte>.Empty,
                0,
                0);
        }
        int cols = BinaryPrimitives.ReadInt32LittleEndian(payload.Span.Slice(OffsetSize));
        int rows = BinaryPrimitives.ReadInt32LittleEndian(payload.Span.Slice(OffsetSize + sizeof(int)));
        int modeLength = BinaryPrimitives.ReadInt32LittleEndian(payload.Span.Slice(OffsetSize + (2 * sizeof(int))));
        if (modeLength < 0 || modeLength > payload.Length - ResyncHeaderSize)
            throw new InvalidDataException("invalid Resync mode length");
        return new StreamResyncMessage(
            baseOffset,
            payload.Slice(ResyncHeaderSize, modeLength),
            payload.Slice(ResyncHeaderSize + modeLength),
            cols,
            rows);
    }
}
