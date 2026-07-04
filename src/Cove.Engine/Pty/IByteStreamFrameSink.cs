using System;

namespace Cove.Engine.Pty;

public interface IByteStreamFrameSink
{
    void SendStreamData(ulong streamId, ulong offset, ReadOnlySpan<byte> raw);
    void SendResync(ulong streamId, ulong newBaseOffset);
    void SendStreamEnd(ulong streamId, ulong finalOffset, int exitCode);
    void SendError(ulong streamId, string code, string message);
}
