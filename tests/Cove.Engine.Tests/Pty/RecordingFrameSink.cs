using System;
using System.Collections.Generic;
using System.IO;
using Cove.Engine.Pty;

namespace Cove.Engine.Tests.Pty;

internal sealed class RecordingFrameSink : IByteStreamFrameSink
{
    public readonly record struct DataFrame(ulong StreamId, ulong Offset, byte[] Raw);
    public readonly record struct ResyncFrame(ulong StreamId, ulong NewBaseOffset, byte[] TerminalModePreamble, byte[] TerminalCheckpoint, int CheckpointCols, int CheckpointRows);
    public readonly record struct EndFrame(ulong StreamId, ulong FinalOffset, int ExitCode);
    public readonly record struct ErrorFrame(ulong StreamId, string Code, string Message);

    public List<DataFrame> Data { get; } = new();
    public List<ResyncFrame> Resyncs { get; } = new();
    public List<EndFrame> Ends { get; } = new();
    public List<ErrorFrame> Errors { get; } = new();

    public int ResyncCount => Resyncs.Count;

    public void SendStreamData(ulong streamId, ulong offset, ReadOnlySpan<byte> raw)
        => Data.Add(new DataFrame(streamId, offset, raw.ToArray()));

    public void SendResync(ulong streamId, ulong newBaseOffset, ReadOnlySpan<byte> terminalModePreamble, ReadOnlySpan<byte> terminalCheckpoint, int checkpointCols, int checkpointRows)
        => Resyncs.Add(new ResyncFrame(streamId, newBaseOffset, terminalModePreamble.ToArray(), terminalCheckpoint.ToArray(), checkpointCols, checkpointRows));

    public void SendStreamEnd(ulong streamId, ulong finalOffset, int exitCode)
        => Ends.Add(new EndFrame(streamId, finalOffset, exitCode));

    public void SendError(ulong streamId, string code, string message)
        => Errors.Add(new ErrorFrame(streamId, code, message));

    public byte[] AllData()
    {
        var ms = new MemoryStream();
        foreach (var d in Data)
            ms.Write(d.Raw, 0, d.Raw.Length);
        return ms.ToArray();
    }
}
