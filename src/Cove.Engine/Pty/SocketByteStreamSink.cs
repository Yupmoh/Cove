using System;
using System.Buffers.Binary;
using System.IO;
using System.Text.Json;
using Cove.Protocol;

namespace Cove.Engine.Pty;

public sealed class SocketByteStreamSink : IByteStreamFrameSink
{
    private readonly Stream _stream;
    private readonly object _writeLock = new();
    private uint _seq;

    public SocketByteStreamSink(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
    }

    private uint NextSeq()
    {
        uint s = ++_seq;
        if (s == 0)
            s = ++_seq;
        return s;
    }

    public void SendStreamData(ulong streamId, ulong offset, ReadOnlySpan<byte> raw)
    {
        int payloadLen = 8 + raw.Length;
        Span<byte> header = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(header, new FrameHeader(FrameType.StreamData, streamId, NextSeq(), (uint)payloadLen));
        Span<byte> off = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(off, offset);
        lock (_writeLock)
        {
            _stream.Write(header);
            _stream.Write(off);
            _stream.Write(raw);
            _stream.Flush();
        }
    }

    public void SendResync(ulong streamId, ulong newBaseOffset)
    {
        Span<byte> header = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(header, new FrameHeader(FrameType.Resync, streamId, NextSeq(), 8));
        Span<byte> payload = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(payload, newBaseOffset);
        lock (_writeLock)
        {
            _stream.Write(header);
            _stream.Write(payload);
            _stream.Flush();
        }
    }

    public void SendStreamEnd(ulong streamId, ulong finalOffset, int exitCode)
    {
        Span<byte> header = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(header, new FrameHeader(FrameType.StreamEnd, streamId, NextSeq(), 12));
        Span<byte> payload = stackalloc byte[12];
        BinaryPrimitives.WriteUInt64LittleEndian(payload.Slice(0, 8), finalOffset);
        BinaryPrimitives.WriteInt32LittleEndian(payload.Slice(8, 4), exitCode);
        lock (_writeLock)
        {
            _stream.Write(header);
            _stream.Write(payload);
            _stream.Flush();
        }
    }

    public void SendError(ulong streamId, string code, string message)
    {
        var frame = new ControlErrorFrame(code, message, streamId);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(frame, CoveJsonContext.Default.ControlErrorFrame);
        Span<byte> header = stackalloc byte[ProtocolConstants.HeaderSize];
        FrameHeader.Write(header, new FrameHeader(FrameType.Error, 0, NextSeq(), (uint)json.Length));
        lock (_writeLock)
        {
            _stream.Write(header);
            _stream.Write(json);
            _stream.Flush();
        }
    }
}
