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
                long baseOffset = _head + (data.Length - _buffer.Length);
                int copyStart = (int)(baseOffset & _mask);
                int copyFirstRun = Math.Min(_buffer.Length - copyStart, last.Length);
                last.Slice(0, copyFirstRun).CopyTo(_buffer.AsSpan(copyStart));
                if (copyFirstRun < last.Length)
                    last.Slice(copyFirstRun).CopyTo(_buffer.AsSpan(0));
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

    public bool ContainsOffset(long offset)
    {
        lock (_sync)
            return offset >= Math.Max(0, _head - _buffer.Length) && offset <= _head;
    }

    public byte[] Snapshot()
    {
        lock (_sync)
        {
            var tail = Math.Max(0, _head - _buffer.Length);
            return SnapshotFromLocked(tail);
        }
    }

    public bool TrySnapshotFrom(long fromOffset, out byte[] bytes)
    {
        lock (_sync)
        {
            var tail = Math.Max(0, _head - _buffer.Length);
            if (fromOffset < tail || fromOffset > _head)
            {
                bytes = Array.Empty<byte>();
                return false;
            }
            bytes = SnapshotFromLocked(fromOffset);
            return true;
        }
    }

    private byte[] SnapshotFromLocked(long fromOffset)
    {
        var length = checked((int)(_head - fromOffset));
        if (length == 0)
            return Array.Empty<byte>();
        var bytes = new byte[length];
        var start = (int)(fromOffset & _mask);
        var firstRun = Math.Min(_buffer.Length - start, length);
        _buffer.AsSpan(start, firstRun).CopyTo(bytes);
        if (firstRun < length)
            _buffer.AsSpan(0, length - firstRun).CopyTo(bytes.AsSpan(firstRun));
        return bytes;
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
