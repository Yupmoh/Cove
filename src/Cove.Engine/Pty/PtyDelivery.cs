using System;
using Cove.Platform.Pty;

namespace Cove.Engine.Pty;

public sealed class PtyDelivery
{
    private readonly long _sessionId;
    private readonly PtyRingBuffer _ring;
    private readonly PtyClientCursor _cursor;
    private readonly IPtyDeliverySink _sink;
    private readonly byte[] _scratch;

    public PtyDelivery(long sessionId, PtyRingBuffer ring, PtyClientCursor cursor, IPtyDeliverySink sink, int scratchBytes = PtyConstants.ReadBufferBytes)
    {
        _sessionId = sessionId;
        _ring = ring;
        _cursor = cursor;
        _sink = sink;
        _scratch = new byte[scratchBytes];
    }

    public void PumpAvailable()
    {
        while (true)
        {
            long head = _ring.Head;
            if (_cursor.Offset >= head)
                return;

            var result = _ring.ReadInto(_cursor.Offset, _scratch);
            if (result.Underrun)
            {
                _cursor.Offset = result.NextOffset;
                _sink.OnResync(_sessionId, result.NextOffset);
                continue;
            }

            if (result.BytesCopied == 0)
                return;

            _sink.OnData(_sessionId, _scratch.AsSpan(0, result.BytesCopied));
            _cursor.Offset = result.NextOffset;
        }
    }
}
