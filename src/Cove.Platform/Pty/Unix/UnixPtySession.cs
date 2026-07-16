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
    private int _firstReadLogged;

    private readonly bool _adopted;

    internal UnixPtySession(long sessionId, int masterFd, int pid, ILogger logger, bool adopted = false)
    {
        SessionId = sessionId;
        _masterFd = masterFd;
        _pid = pid;
        _logger = logger;
        _adopted = adopted;
    }

    internal int MasterFd => _masterFd;
    internal int Pid => _pid;

    public long SessionId { get; }
    public bool HasExited => Volatile.Read(ref _hasExited) != 0;
    public int ExitCode => HasExited ? _exitCode : -1;

    public int Read(Span<byte> buffer)
    {
        nint n = CovePtyNative.Read(_masterFd, buffer, buffer.Length);
        if (n >= 0)
        {
            if (Interlocked.Exchange(ref _firstReadLogged, 1) == 0)
                _logger.UnixFirstRead(SessionId, (int)n);
            else
                _logger.UnixRead(SessionId, (int)n);
            return (int)n;
        }
        _logger.UnixReadFailed(SessionId, (int)-n);
        throw new PtyIoException($"pty read failed (session {SessionId}, errno {-n}).", (int)-n);
    }

    public bool WaitReadable(int timeoutMs)
    {
        var rc = CovePtyNative.PollReadable(_masterFd, timeoutMs);
        if (rc < 0)
            return true;
        return rc == 1;
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return;
        if (data.Length > PtyConstants.MaxWriteBytes)
            throw new ArgumentException($"write exceeds {PtyConstants.MaxWriteBytes} bytes.", nameof(data));
        nint n = CovePtyNative.Write(_masterFd, data, data.Length);
        if (n < 0)
        {
            _logger.UnixWriteFailed(SessionId, data.Length, (int)-n);
            throw new PtyIoException($"pty write failed (session {SessionId}, errno {-n}).", (int)-n);
        }
        _logger.UnixWrite(SessionId, (int)n);
    }

    public void Resize(int cols, int rows)
    {
        ushort c = (ushort)Math.Clamp(cols, PtyConstants.MinCols, PtyConstants.MaxCols);
        ushort r = (ushort)Math.Clamp(rows, PtyConstants.MinRows, PtyConstants.MaxRows);
        _logger.UnixResizeRequested(SessionId, cols, rows);
        if (c != cols || r != rows)
            _logger.UnixResizeClamped(SessionId, cols, rows, c, r);
        int rc = CovePtyNative.Resize(_masterFd, c, r);
        if (rc != 0)
            _logger.UnixResizeFailed(SessionId, -rc);
    }

    public void Kill()
    {
        if (Interlocked.Exchange(ref _killed, 1) != 0)
            return;
        _logger.UnixKillRequested(SessionId);
        int rc = CovePtyNative.Kill(_pid, PtyConstants.SigKill);
        if (rc != 0 && -rc != PtyConstants.Esrch)
            _logger.UnixKillFailed(SessionId, -rc);
    }

    public bool Signal(int signum)
    {
        _logger.UnixSignalRequested(SessionId, signum);
        int rc = CovePtyNative.Kill(_pid, signum);
        if (rc != 0 && -rc != PtyConstants.Esrch)
        {
            _logger.UnixSignalFailed(SessionId, signum, -rc);
            return false;
        }
        return true;
    }

    public int WaitForExit()
    {
        if (HasExited)
            return _exitCode;
        if (_adopted)
        {
            try
            {
                if (!ProcessExitWatch.WaitForExitAsync(_pid).Wait(TimeSpan.FromSeconds(2)))
                    _logger.UnixAdoptedExitUnobservable(SessionId, _pid);
            }
            catch (AggregateException)
            {
                _logger.UnixAdoptedExitUnobservable(SessionId, _pid);
            }
            _exitCode = -1;
            Volatile.Write(ref _hasExited, 1);
            _logger.SessionExited(SessionId, _exitCode);
            return -1;
        }
        _logger.UnixWaitBegin(SessionId);
        for (int i = 0; i < 2000; i++)
        {
            int rc = CovePtyNative.Reap(_pid);
            if (rc == -1) { Thread.Sleep(1); continue; }
            _exitCode = rc < -1 ? -1 : rc;
            Volatile.Write(ref _hasExited, 1);
            _logger.UnixReaped(SessionId, _exitCode);
            _logger.SessionExited(SessionId, _exitCode);
            return _exitCode;
        }
        _exitCode = -1;
        Volatile.Write(ref _hasExited, 1);
        _logger.UnixReapTimeout(SessionId, _pid);
        _logger.SessionExited(SessionId, _exitCode);
        return -1;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _logger.UnixDisposeClose(SessionId);
        CovePtyNative.Close(_masterFd);
    }
}
