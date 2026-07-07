using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Cove.Platform.Pty.Unix;

public sealed class UnixPtySession : IPtySession
{
    private readonly ILogger _logger;
    private readonly int _masterFd;
    private readonly int _pid;
    private int _killed;
    private int _hasExited;
    private int _exitCode = -1;
    private int _disposed;

    internal UnixPtySession(long sessionId, int masterFd, int pid, ILogger logger)
    {
        SessionId = sessionId;
        _masterFd = masterFd;
        _pid = pid;
        _logger = logger;
    }

    public long SessionId { get; }
    public bool HasExited => Volatile.Read(ref _hasExited) != 0;
    public int ExitCode => HasExited ? _exitCode : -1;

    public int Read(Span<byte> buffer)
    {
        nint n = CovePtyNative.Read(_masterFd, buffer, buffer.Length);
        if (n >= 0)
            return (int)n;
        throw new PtyIoException($"pty read failed (session {SessionId}, errno {-n}).", (int)-n);
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return;
        if (data.Length > PtyConstants.MaxWriteBytes)
            throw new ArgumentException($"write exceeds {PtyConstants.MaxWriteBytes} bytes.", nameof(data));
        nint n = CovePtyNative.Write(_masterFd, data, data.Length);
        if (n < 0)
            throw new PtyIoException($"pty write failed (session {SessionId}, errno {-n}).", (int)-n);
    }

    public void Resize(int cols, int rows)
    {
        ushort c = (ushort)Math.Clamp(cols, PtyConstants.MinCols, PtyConstants.MaxCols);
        ushort r = (ushort)Math.Clamp(rows, PtyConstants.MinRows, PtyConstants.MaxRows);
        if (c != cols || r != rows)
            _logger.LogWarning("pty resize clamped ({Cols}x{Rows} -> {C}x{R}) session {Id}.", cols, rows, c, r, SessionId);
        int rc = CovePtyNative.Resize(_masterFd, c, r);
        if (rc != 0)
            _logger.LogWarning("pty resize ioctl failed (session {Id}, errno {Errno}).", SessionId, -rc);
    }

    public void Kill()
    {
        if (Interlocked.Exchange(ref _killed, 1) != 0)
            return;
        int rc = CovePtyNative.Kill(_pid, PtyConstants.SigKill);
        if (rc != 0 && -rc != PtyConstants.Esrch)
            _logger.LogWarning("pty kill failed (session {Id}, errno {Errno}).", SessionId, -rc);
    }

    public bool Signal(int signum)
    {
        int rc = CovePtyNative.Kill(_pid, signum);
        if (rc != 0 && -rc != PtyConstants.Esrch)
        {
            _logger.LogWarning("pty signal {sig} failed (session {Id}, errno {Errno}).", signum, SessionId, -rc);
            return false;
        }
        return true;
    }

    public int WaitForExit()
    {
        if (HasExited)
            return _exitCode;
        for (int i = 0; i < 2000; i++)
        {
            int rc = CovePtyNative.Reap(_pid);
            if (rc == -1) { Thread.Sleep(1); continue; }
            _exitCode = rc < -1 ? -1 : rc;
            Volatile.Write(ref _hasExited, 1);
            return _exitCode;
        }
        _exitCode = -1;
        Volatile.Write(ref _hasExited, 1);
        return -1;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        CovePtyNative.Close(_masterFd);
    }
}
