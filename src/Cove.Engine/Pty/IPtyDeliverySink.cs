using System;

namespace Cove.Engine.Pty;

public interface IPtyDeliverySink
{
    void OnData(long sessionId, ReadOnlySpan<byte> bytes);
    void OnResync(long sessionId, long fromOffset);
}
