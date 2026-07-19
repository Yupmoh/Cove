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
    private readonly UnixPtyExitPolicy _exitPolicy;
    private readonly CancellationTokenSource? _exitWatchCancellation;
    private readonly Task<int>? _exitWatch;
    private int _killed;
    private int _exitState;
    private int _exitCode = -1;
    private int _disposed;
    private int _firstReadLogged;

    internal UnixPtySession(
        long sessionId,
        int masterFd,
        int pid,
        ILogger logger,
        bool adopted = false,
        UnixPtyExitPolicy? exitPolicy = null)
    {
        SessionId = sessionId;
        _masterFd = masterFd;
        _pid = pid;
        _logger = logger;
        _adopted = adopted;
        _exitPolicy = exitPolicy ?? UnixPtyExitPolicy.Default;
        if (adopted)
        {
            _exitWatchCancellation = new CancellationTokenSource();
            try
            {
                _exitWatch = _exitPolicy.ObserveExitAsync(pid, _exitWatchCancellation.Token);
            }
            catch (Exception exception)
            {
                Volatile.Write(ref _exitState, (int)ExitObservationState.WatcherCreationFailed);
                _logger.UnixExitWatcherCreationFailed(SessionId, _pid, exception.Message, exception);
                _exitWatch = Task.FromException<int>(exception);
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
            if (ProcessExitWatch.TryObserveExit(_exitWatch!, _exitPolicy.AdoptedExitTimeout, out var exitCode))
                return PublishExit(exitCode, false);
            _logger.UnixAdoptedExitTimedOut(
                SessionId,
                _pid,
                (long)_exitPolicy.AdoptedExitTimeout.TotalMilliseconds);
            return -1;
        }
        catch (OperationCanceledException exception)
        {
            Volatile.Write(ref _exitState, (int)ExitObservationState.ObservationCanceled);
            _logger.UnixAdoptedExitCanceled(SessionId, _pid, exception.Message, exception);
        }
        catch (Exception exception)
        {
            if (Volatile.Read(ref _exitState) != (int)ExitObservationState.WatcherCreationFailed)
                Volatile.Write(ref _exitState, (int)ExitObservationState.ObservationFailed);
            _logger.UnixAdoptedExitFailed(SessionId, _pid, exception.Message, exception);
        }
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
        _exitWatchCancellation?.Cancel();
        _exitWatchCancellation?.Dispose();
        _logger.UnixDisposeClose(SessionId);
        CovePtyNative.Close(_masterFd);
    }
    private enum ExitObservationState
    {
        Running,
        Exited,
        ObservationUnknown,
        WatcherCreationFailed,
        ObservationCanceled,
        ObservationFailed,
    }
}

internal static partial class UnixPtySessionLog
{
    [ZLoggerMessage(LogLevel.Error, "pty poll failed session={sessionId} fd={fd} errno={errno}", EventId = 1096)]
    public static partial void UnixPollFailed(this ILogger logger, long sessionId, int fd, int errno);

    [ZLoggerMessage(LogLevel.Error, "pty reap failed session={sessionId} pid={pid} errno={errno}", EventId = 1097)]
    public static partial void UnixReapFailed(this ILogger logger, long sessionId, int pid, int errno);

    [ZLoggerMessage(LogLevel.Error, "pty exit watcher creation failed session={sessionId} pid={pid} error={error}", EventId = 1098)]
    public static partial void UnixExitWatcherCreationFailed(
        this ILogger logger,
        long sessionId,
        int pid,
        string error,
        Exception exception);

    [ZLoggerMessage(LogLevel.Warning, "pty adopted exit observation timed out session={sessionId} pid={pid} timeoutMs={timeoutMs}", EventId = 1099)]
    public static partial void UnixAdoptedExitTimedOut(
        this ILogger logger,
        long sessionId,
        int pid,
        long timeoutMs);

    [ZLoggerMessage(LogLevel.Warning, "pty adopted exit observation canceled session={sessionId} pid={pid} error={error}", EventId = 1100)]
    public static partial void UnixAdoptedExitCanceled(
        this ILogger logger,
        long sessionId,
        int pid,
        string error,
        Exception exception);

    [ZLoggerMessage(LogLevel.Error, "pty adopted exit observation failed session={sessionId} pid={pid} error={error}", EventId = 1101)]
    public static partial void UnixAdoptedExitFailed(
        this ILogger logger,
        long sessionId,
        int pid,
        string error,
        Exception exception);
}

internal sealed class UnixPtyExitPolicy
{
    internal static UnixPtyExitPolicy Default { get; } =
        new(TimeSpan.FromSeconds(2), ProcessExitWatch.WaitForExitAsync);

    internal UnixPtyExitPolicy(
        TimeSpan adoptedExitTimeout,
        Func<int, CancellationToken, Task<int>> observeExitAsync)
    {
        if (adoptedExitTimeout < TimeSpan.Zero && adoptedExitTimeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(adoptedExitTimeout));
        AdoptedExitTimeout = adoptedExitTimeout;
        ObserveExitAsync = observeExitAsync ?? throw new ArgumentNullException(nameof(observeExitAsync));
    }

    internal TimeSpan AdoptedExitTimeout { get; }
    internal Func<int, CancellationToken, Task<int>> ObserveExitAsync { get; }
}
