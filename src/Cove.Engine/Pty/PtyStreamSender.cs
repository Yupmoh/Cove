using System;
using Cove.Protocol;

namespace Cove.Engine.Pty;

public sealed class PtyStreamSender
{
    private readonly ulong _streamId;
    private readonly long _sessionId;
    private readonly PtyRingBuffer _ring;
    private readonly IByteStreamFrameSink _sink;
    private readonly byte[] _scratch;

    private long _sentOffset;
    private long _ackOffset;
    private bool _childExited;
    private int _exitCode = -1;
    private bool _ended;
    private bool _faulted;

    public PtyStreamSender(ulong streamId, long sessionId, PtyRingBuffer ring, long baseOffset, IByteStreamFrameSink sink)
    {
        if (streamId == 0)
            throw new ArgumentOutOfRangeException(nameof(streamId), "byte-stream id must be >= 1.");
        if (baseOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(baseOffset));
        ArgumentNullException.ThrowIfNull(ring);
        ArgumentNullException.ThrowIfNull(sink);
        _streamId = streamId;
        _sessionId = sessionId;
        _ring = ring;
        _sink = sink;
        _scratch = new byte[ProtocolConstants.StreamDataMaxRawBytes];
        _sentOffset = baseOffset;
        _ackOffset = baseOffset;
    }

    public ulong StreamId => _streamId;
    public long SessionId => _sessionId;
    public long SentOffset => _sentOffset;
    public long AckOffset => _ackOffset;
    public bool Ended => _ended;
    public bool Faulted => _faulted;

    public void MarkChildExited(int exitCode)
    {
        _childExited = true;
        _exitCode = exitCode;
    }

    public void OnCredit(ulong ackOffset)
    {
        if (_faulted || _ended)
            return;
        long ack = (long)ackOffset;
        if (ack < _ackOffset || ack > _sentOffset)
        {
            _faulted = true;
            _sink.SendError(_streamId, "invalid_credit",
                $"ackOffset {ack} out of range [{_ackOffset},{_sentOffset}] on stream {_streamId}");
            return;
        }
        _ackOffset = ack;
        PumpAvailable();
    }

    public void PumpAvailable()
    {
        if (_faulted || _ended)
            return;

        while (true)
        {
            long head = _ring.Head;
            long tail = _ring.Tail;

            if (_sentOffset < tail)
            {
                long newBase = tail;
                _sink.SendResync(_streamId, (ulong)newBase);
                _sentOffset = newBase;
                _ackOffset = newBase;
                continue;
            }

            long inflight = _sentOffset - _ackOffset;
            if (inflight >= ProtocolConstants.FlowWindow)
                return;

            if (_sentOffset < head)
            {
                long room = ProtocolConstants.FlowWindow - inflight;
                int want = (int)Math.Min(Math.Min(room, ProtocolConstants.StreamDataMaxRawBytes), head - _sentOffset);
                RingReadResult result = _ring.ReadInto(_sentOffset, _scratch.AsSpan(0, want));
                if (result.Underrun)
                {
                    long nb = result.NextOffset;
                    _sink.SendResync(_streamId, (ulong)nb);
                    _sentOffset = nb;
                    _ackOffset = nb;
                    continue;
                }
                if (result.BytesCopied == 0)
                    return;
                _sink.SendStreamData(_streamId, (ulong)_sentOffset, _scratch.AsSpan(0, result.BytesCopied));
                _sentOffset = result.NextOffset;
                continue;
            }

            if (_childExited)
            {
                _sink.SendStreamEnd(_streamId, (ulong)head, _exitCode);
                _ended = true;
            }
            return;
        }
    }
}
