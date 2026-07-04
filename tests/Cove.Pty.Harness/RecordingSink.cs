using System;
using System.IO;
using Cove.Engine.Pty;

namespace Cove.Pty.Harness;

internal sealed class RecordingSink : IPtyDeliverySink
{
    private readonly MemoryStream _data = new();
    public int ResyncCount { get; private set; }
    public long LastResyncOffset { get; private set; } = -1;

    public void OnData(long sessionId, ReadOnlySpan<byte> bytes) => _data.Write(bytes);

    public void OnResync(long sessionId, long fromOffset)
    {
        ResyncCount++;
        LastResyncOffset = fromOffset;
        _data.SetLength(0);
    }

    public byte[] Delivered => _data.ToArray();
}
