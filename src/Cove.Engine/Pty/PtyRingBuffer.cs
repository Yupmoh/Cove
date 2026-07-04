using System;
using System.Numerics;
using Cove.Platform.Pty;

namespace Cove.Engine.Pty;

public sealed class PtyRingBuffer
{
    private readonly byte[] _buffer;
    private readonly int _mask;
    private readonly object _sync = new();
    private long _head;

    public PtyRingBuffer(int capacity = PtyConstants.DefaultRingCapacityBytes)
    {
        if (capacity < PtyConstants.MinRingCapacityBytes)
            throw new ArgumentException($"ring capacity must be >= {PtyConstants.MinRingCapacityBytes}.", nameof(capacity));
        if (!BitOperations.IsPow2(capacity))
            throw new ArgumentException("ring capacity must be a power of two.", nameof(capacity));
        _buffer = new byte[capacity];
        _mask = capacity - 1;
    }

    public int Capacity => _buffer.Length;
    public long Head { get { lock (_sync) return _head; } }
    public long Tail { get { lock (_sync) return Math.Max(0, _head - _buffer.Length); } }

    public void Append(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return;
        lock (_sync)
        {
            if (data.Length >= _buffer.Length)
            {
                var last = data.Slice(data.Length - _buffer.Length);
                last.CopyTo(_buffer);
                _head += data.Length;
                return;
            }
            int start = (int)(_head & _mask);
            int firstRun = Math.Min(_buffer.Length - start, data.Length);
            data.Slice(0, firstRun).CopyTo(_buffer.AsSpan(start));
            if (firstRun < data.Length)
                data.Slice(firstRun).CopyTo(_buffer.AsSpan(0));
            _head += data.Length;
        }
    }

    public RingReadResult ReadInto(long fromOffset, Span<byte> dest)
    {
        lock (_sync)
        {
            long tail = Math.Max(0, _head - _buffer.Length);
            if (fromOffset > _head)
                throw new ArgumentOutOfRangeException(nameof(fromOffset), "offset is ahead of the stream head.");
            if (fromOffset < tail)
                return new RingReadResult { BytesCopied = 0, NextOffset = tail, Underrun = true };

            long available = _head - fromOffset;
            int toCopy = (int)Math.Min(available, dest.Length);
            if (toCopy == 0)
                return new RingReadResult { BytesCopied = 0, NextOffset = fromOffset, Underrun = false };

            int start = (int)(fromOffset & _mask);
            int firstRun = Math.Min(_buffer.Length - start, toCopy);
            _buffer.AsSpan(start, firstRun).CopyTo(dest);
            if (firstRun < toCopy)
                _buffer.AsSpan(0, toCopy - firstRun).CopyTo(dest.Slice(firstRun));

            return new RingReadResult { BytesCopied = toCopy, NextOffset = fromOffset + toCopy, Underrun = false };
        }
    }
}
