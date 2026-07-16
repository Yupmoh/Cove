using System;

namespace Cove.Platform.Pty;

public interface IPtySession : IDisposable
{
    long SessionId { get; }
    bool HasExited { get; }
    int ExitCode { get; }
    int Read(Span<byte> buffer);
    void Write(ReadOnlySpan<byte> data);
    void Resize(int cols, int rows);
    void Kill();
    bool Signal(int signum);
    int WaitForExit();

    bool WaitReadable(int timeoutMs) => true;
}
