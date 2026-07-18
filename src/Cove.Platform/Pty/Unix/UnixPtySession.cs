using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Platform.Pty.Unix;

public sealed class UnixPtySession : IPtySession
{
    private readonly ILogger _logger;
    private readonly int _masterFd;
    private readonly int _pid;
    private readonly object _exitGate = new();
    private readonly bool _adopted;
    private readonly Task<int>? _exitWatch;
    private int _killed;
    private int _exitState;
    private int _exitCode = -1;
    private int _disposed;
    private int _firstReadLogged;

    internal UnixPtySession(long sessionId, int masterFd, int pid, ILogger logger, bool adopted = false)
    {
        SessionId = sessionId;
        _masterFd = masterFd;
        _pid = pid;
        _logger = logger;
        _adopted = adopted;
        if (adopted)
        {
            try
            {
                _exitWatch = ProcessExitWatch.WaitForExitAsync(pid);
            }
            catch (PtyIoException)
            {
                _exitWatch = null;
            }
        }
    }

    internal int MasterFd => _masterFd;
    internal int Pid => _pid;

    public long SessionId { get; }
    public bool HasExited => Volatile.Read(ref _exitState) == (int)ExitObservationState.Exited;
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
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(UnixPtySession));
        var rc = CovePtyNative.PollReadable(_masterFd, timeoutMs);
        if (rc >= 0)
            return rc == 1;
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(UnixPtySession));
        var errno = -rc;
        _logger.UnixPollFailed(SessionId, _masterFd, errno);
        throw new PtyIoException($"pty poll failed (session {SessionId}, fd {_masterFd}, errno {errno}).", errno);
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
        lock (_exitGate)
        {
            if (HasExited)
                return _exitCode;
            _logger.UnixWaitBegin(SessionId);
            return _adopted ? WaitForAdoptedExit() : WaitForOwnedExit();
        }
    }

    private int WaitForAdoptedExit()
    {
        try
        {
            var observation = _exitWatch ?? ProcessExitWatch.WaitForExitAsync(_pid);
            if (ProcessExitWatch.TryObserveExit(observation, TimeSpan.FromSeconds(2), out var exitCode))
                return PublishExit(exitCode, false);
        }
        catch (AggregateException)
        {
        }
        catch (PtyIoException)
        {
        }
        Volatile.Write(ref _exitState, (int)ExitObservationState.ObservationUnknown);
        _logger.UnixAdoptedExitUnobservable(SessionId, _pid);
        return -1;
    }

    private int WaitForOwnedExit()
    {
        var exitCode = CovePtyNative.Reap(_pid);
        if (exitCode < 0)
        {
            Volatile.Write(ref _exitState, (int)ExitObservationState.ObservationUnknown);
            _logger.UnixReapFailed(SessionId, _pid, -exitCode);
            return -1;
        }
        return PublishExit(exitCode, true);
    }

    private int PublishExit(int exitCode, bool reaped)
    {
        _exitCode = exitCode;
        Volatile.Write(ref _exitState, (int)ExitObservationState.Exited);
        if (reaped)
            _logger.UnixReaped(SessionId, exitCode);
        _logger.SessionExited(SessionId, exitCode);
        return exitCode;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _logger.UnixDisposeClose(SessionId);
        CovePtyNative.Close(_masterFd);
    }
    private enum ExitObservationState
    {
        Running,
        Exited,
        ObservationUnknown,
    }
}

internal static partial class UnixPtySessionLog
{
    [ZLoggerMessage(LogLevel.Error, "pty poll failed session={sessionId} fd={fd} errno={errno}", EventId = 1096)]
    public static partial void UnixPollFailed(this ILogger logger, long sessionId, int fd, int errno);

    [ZLoggerMessage(LogLevel.Error, "pty reap failed session={sessionId} pid={pid} errno={errno}", EventId = 1097)]
    public static partial void UnixReapFailed(this ILogger logger, long sessionId, int pid, int errno);
}
