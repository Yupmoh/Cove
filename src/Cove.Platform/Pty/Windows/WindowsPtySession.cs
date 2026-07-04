using System;

namespace Cove.Platform.Pty.Windows;

public sealed class WindowsPtySession : IPtySession
{
    public long SessionId { get; }
    public bool HasExited => false;
    public int ExitCode => -1;

    internal WindowsPtySession(long sessionId) => SessionId = sessionId;

    public int Read(Span<byte> buffer) => throw new NotImplementedException("ConPTY read wired in M0 T7.");
    public void Write(ReadOnlySpan<byte> data) => throw new NotImplementedException("ConPTY write wired in M0 T7.");
    public void Resize(int cols, int rows) => throw new NotImplementedException("ConPTY resize wired in M0 T7.");
    public void Kill() => throw new NotImplementedException("ConPTY kill wired in M0 T7.");
    public int WaitForExit() => throw new NotImplementedException("ConPTY wait wired in M0 T7.");
    public void Dispose() { }
}
