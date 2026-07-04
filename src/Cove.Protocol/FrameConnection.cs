using System.Buffers;
using System.IO.Pipelines;

namespace Cove.Protocol;

public sealed class FrameConnection : IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly PipeReader _reader;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private uint _sendSeq;

    public FrameConnection(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _reader = PipeReader.Create(stream);
    }

    public async ValueTask<Frame?> ReadFrameAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            ReadResult result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            ReadOnlySequence<byte> buffer = result.Buffer;

            if (TryParseFrame(ref buffer, out Frame frame))
            {
                _reader.AdvanceTo(buffer.Start);
                return frame;
            }

            _reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
            {
                if (buffer.Length == 0)
                    return null;
                throw new ProtocolException("malformed_frame", "connection closed mid-frame");
            }
        }
    }

    private static bool TryParseFrame(ref ReadOnlySequence<byte> buffer, out Frame frame)
    {
        frame = default;
        if (buffer.Length < ProtocolConstants.HeaderSize)
            return false;

        Span<byte> headerBytes = stackalloc byte[ProtocolConstants.HeaderSize];
        buffer.Slice(0, ProtocolConstants.HeaderSize).CopyTo(headerBytes);

        if (!FrameHeader.TryRead(headerBytes, out FrameHeader header, out string? error))
        {
            if (error == "short_header")
                return false;
            throw new ProtocolException(error!, $"frame rejected: {error}");
        }

        long totalNeeded = ProtocolConstants.HeaderSize + header.Length;
        if (buffer.Length < totalNeeded)
            return false;

        byte[] payload = header.Length == 0
            ? Array.Empty<byte>()
            : buffer.Slice(ProtocolConstants.HeaderSize, header.Length).ToArray();

        buffer = buffer.Slice(totalNeeded);
        frame = new Frame(header, payload);
        return true;
    }

    public async ValueTask WriteFrameAsync(
        FrameType type, ulong streamId, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (payload.Length > ProtocolConstants.MaxFramePayload)
            throw new ProtocolException("frame_too_large",
                $"payload {payload.Length} exceeds {ProtocolConstants.MaxFramePayload}");

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            uint seq = NextSeq();
            byte[] header = new byte[ProtocolConstants.HeaderSize];
            FrameHeader.Write(header, new FrameHeader(type, streamId, seq, (uint)payload.Length));
            await _stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            if (!payload.IsEmpty)
                await _stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private uint NextSeq()
    {
        uint next = _sendSeq + 1;
        if (next == 0)
            next = 1;
        _sendSeq = next;
        return next;
    }

    public async ValueTask DisposeAsync()
    {
        await _reader.CompleteAsync().ConfigureAwait(false);
        _writeLock.Dispose();
    }
}
