using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace Cove.Platform.Pty.Windows;

public sealed class WindowsPtySession : IPtySession
{
    private const int PostExitFlushMilliseconds = 1000;
    private const int OutputQuiesceMilliseconds = 150;
    private const int PostExitDrainCapMilliseconds = 3000;
    private const int DisposeTimeoutMilliseconds = 3000;

    private readonly ILogger _logger;
    private readonly SafeFileHandle _outputRead;
    private readonly SafeFileHandle _inputWrite;
    private readonly bool _suppressWatcherClose;
    private readonly SafeFileHandle? _conptyInputRead;
    private readonly SafeFileHandle? _conptyOutputWrite;
    private readonly IntPtr _pseudoConsole;
    private readonly IntPtr _processHandle;
    private readonly IntPtr _threadHandle;
    private readonly int _processId;
    private readonly ManualResetEventSlim _exitEvent = new(false);
    private readonly ManualResetEventSlim _disposeRequested = new(false);
    private readonly Thread _exitWatcher;
    private readonly WindowsPtySessionTestHooks? _testHooks;

    private int _hasExited;
    private int _exitCode = -1;
    private int _consoleClosed;
    private int _killed;
    private int _watcherCleanupState;
    private int _sessionResourcesClosed;
    private int _disposed;
    private int _firstReadLogged;
    private long _lastReadTicks;

    internal WindowsPtySession(
        long sessionId,
        IntPtr pseudoConsole,
        SafeFileHandle outputRead,
        SafeFileHandle inputWrite,
        IntPtr processHandle,
        IntPtr threadHandle,
        int processId,
        ILogger logger,
        bool suppressWatcherClose = false,
        SafeFileHandle? conptyInputRead = null,
        SafeFileHandle? conptyOutputWrite = null,
        WindowsPtySessionTestHooks? testHooks = null)
    {
        SessionId = sessionId;
        _pseudoConsole = pseudoConsole;
        _outputRead = outputRead;
        _inputWrite = inputWrite;
        _suppressWatcherClose = suppressWatcherClose;
        _conptyInputRead = conptyInputRead;
        _conptyOutputWrite = conptyOutputWrite;
        _processHandle = processHandle;
        _threadHandle = threadHandle;
        _processId = processId;
        _logger = logger;
        _testHooks = testHooks;

        _exitWatcher = new Thread(WatchForExit)
        {
            IsBackground = true,
            Name = $"cove-conpty-exit-{sessionId}",
        };
        _exitWatcher.Start();
        _logger.WinExitWatcherStarted(sessionId);
    }

    public long SessionId { get; }
    public bool HasExited => Volatile.Read(ref _hasExited) != 0;
    public int ExitCode => HasExited ? _exitCode : -1;

    public int Read(Span<byte> buffer)
    {
        if (buffer.IsEmpty)
            return 0;
        if (Volatile.Read(ref _disposed) != 0)
            return 0;
        if (ConPtyNative.ReadFile(_outputRead, buffer, buffer.Length, out int read, IntPtr.Zero))
        {
            if (read > 0)
                Volatile.Write(ref _lastReadTicks, Environment.TickCount64);
            if (Interlocked.Exchange(ref _firstReadLogged, 1) == 0)
                _logger.WinFirstRead(SessionId, read);
            else
                _logger.WinRead(SessionId, read);
            return read;
        }
        int error = Marshal.GetLastPInvokeError();
        if (error is ConPtyNative.ErrorBrokenPipe
            or ConPtyNative.ErrorHandleEof
            or ConPtyNative.ErrorOperationAborted
            or ConPtyNative.ErrorNoData)
        {
            _logger.WinReadEof(SessionId, error);
            return 0;
        }
        if (Volatile.Read(ref _disposed) != 0)
            return 0;
        _logger.WinReadFailed(SessionId, error);
        throw new PtyIoException($"conpty read failed (session {SessionId}, error {error}).", error);
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return;
        if (data.Length > PtyConstants.MaxWriteBytes)
            throw new ArgumentException($"write exceeds {PtyConstants.MaxWriteBytes} bytes.", nameof(data));
        _logger.WinWriteBegin(SessionId, data.Length);
        int offset = 0;
        while (offset < data.Length)
        {
            var slice = data.Slice(offset);
            if (!ConPtyNative.WriteFile(_inputWrite, slice, slice.Length, out int written, IntPtr.Zero))
            {
                int error = Marshal.GetLastPInvokeError();
                _logger.WinWriteFailed(SessionId, offset, error);
                throw new PtyIoException($"conpty write failed (session {SessionId}, error {error}).", error);
            }
            if (written <= 0)
            {
                _logger.WinWriteNoProgress(SessionId, offset);
                break;
            }
            offset += written;
            _logger.WinWriteChunk(SessionId, written, offset);
        }
    }

    public void Resize(int cols, int rows)
    {
        ushort c = (ushort)Math.Clamp(cols, PtyConstants.MinCols, PtyConstants.MaxCols);
        ushort r = (ushort)Math.Clamp(rows, PtyConstants.MinRows, PtyConstants.MaxRows);
        _logger.WinResizeRequested(SessionId, cols, rows);
        if (c != cols || r != rows)
            _logger.WinResizeClamped(SessionId, cols, rows, c, r);
        if (Volatile.Read(ref _consoleClosed) != 0)
        {
            _logger.WinResizeSkippedClosed(SessionId);
            return;
        }
        var size = new ConPtyNative.Coord { X = (short)c, Y = (short)r };
        int hr = ConPtyNative.ResizePseudoConsole(_pseudoConsole, size);
        if (hr != 0)
            _logger.WinResizeFailed(SessionId, hr);
    }

    public void Kill()
    {
        if (Interlocked.Exchange(ref _killed, 1) != 0)
            return;
        if (HasExited)
            return;
        _logger.WinKillRequested(SessionId);
        if (_testHooks?.TerminateProcess is { } terminateProcess)
        {
            terminateProcess();
            return;
        }
        if (!ConPtyNative.TerminateProcess(_processHandle, 1))
        {
            int error = Marshal.GetLastPInvokeError();
            _logger.WinKillFailed(SessionId, error);
        }
    }

    public bool Signal(int signum)
    {
        if (signum is PtyConstants.SigKill or PtyConstants.SigTerm)
        {
            Kill();
            return true;
        }
        _logger.WinSignalUnsupported(SessionId, signum);
        return false;
    }

    public int WaitForExit()
    {
        if (!HasExited)
        {
            try
            {
                _exitEvent.Wait();
            }
            catch (ObjectDisposedException) when (HasExited)
            {
            }
        }
        return _exitCode;
    }

    private void WatchForExit()
    {
        try
        {
            if (_testHooks?.WaitForExit is { } waitForExit)
            {
                _exitCode = waitForExit();
            }
            else
            {
                ConPtyNative.WaitForSingleObject(_processHandle, ConPtyNative.Infinite);
                if (ConPtyNative.GetExitCodeProcess(_processHandle, out uint code))
                    _exitCode = unchecked((int)code);
                else
                {
                    _logger.WinGetExitCodeFailed(SessionId, Marshal.GetLastPInvokeError());
                    _exitCode = -1;
                }
            }
            Volatile.Write(ref _hasExited, 1);
            _exitEvent.Set();
            _testHooks?.ExitSignalSet?.Invoke();
            _logger.WinExitObserved(SessionId, _exitCode);
            _logger.SessionExited(SessionId, _exitCode);
            if (!_suppressWatcherClose && Volatile.Read(ref _disposed) == 0)
                DrainPostExitThenClose();
        }
        finally
        {
            if (Interlocked.CompareExchange(ref _watcherCleanupState, 2, 0) == 1)
                CloseWatcherResources();
        }
    }

    private void DrainPostExitThenClose()
    {
        long exitTicks = Environment.TickCount64;
        long deadline = exitTicks + PostExitDrainCapMilliseconds;
        while (Volatile.Read(ref _disposed) == 0)
        {
            long now = Environment.TickCount64;
            long elapsedSinceExit = now - exitTicks;
            long lastRead = Volatile.Read(ref _lastReadTicks);
            long quietFor = lastRead == 0 ? elapsedSinceExit : now - lastRead;
            bool minFlushMet = elapsedSinceExit >= PostExitFlushMilliseconds;
            bool outputQuiesced = quietFor >= OutputQuiesceMilliseconds;
            if (minFlushMet && outputQuiesced)
                break;
            if (now >= deadline)
                break;
            int wait = (int)Math.Min(25, Math.Max(1, deadline - now));
            _disposeRequested.Wait(wait);
        }
        CloseConsole();
    }

    private void CloseConsole()
    {
        if (Interlocked.Exchange(ref _consoleClosed, 1) != 0)
            return;
        _logger.WinPseudoConsoleClosed(SessionId);
        if (_pseudoConsole != IntPtr.Zero)
            ConPtyNative.ClosePseudoConsole(_pseudoConsole);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _disposeRequested.Set();
        _logger.WinDisposeBegin(SessionId, HasExited);
        if (!HasExited)
            Kill();

        TimeSpan timeout = _testHooks?.DisposeTimeout ?? TimeSpan.FromMilliseconds(DisposeTimeoutMilliseconds);
        long deadline = Environment.TickCount64 + Math.Max(0L, (long)timeout.TotalMilliseconds);
        bool exitObserved = _exitEvent.Wait(Remaining(deadline));
        List<Exception>? failures = null;
        try
        {
            CloseSessionResources();
        }
        catch (Exception ex)
        {
            AddFailure(ref failures, ex);
        }

        bool watcherJoined = _exitWatcher.Join(Remaining(deadline));
        if (watcherJoined)
        {
            try
            {
                CloseWatcherResources();
            }
            catch (Exception ex)
            {
                AddFailure(ref failures, ex);
            }
        }
        else
        {
            int cleanupState = Interlocked.CompareExchange(ref _watcherCleanupState, 1, 0);
            if (cleanupState == 2)
            {
                try
                {
                    CloseWatcherResources();
                }
                catch (Exception ex)
                {
                    AddFailure(ref failures, ex);
                }
            }
        }

        if (!exitObserved || !watcherJoined)
        {
            string state = !exitObserved
                ? "process exit was not observed"
                : "the exit watcher did not stop";
            AddFailure(
                ref failures,
                new TimeoutException(
                    $"Timed out after {timeout.TotalMilliseconds:0} ms disposing Windows PTY session {SessionId}: {state}. Retained process and event resources will be closed by the exit watcher when it completes."));
        }

        if (failures is { Count: 1 })
            throw failures[0];
        if (failures is { Count: > 1 })
            throw new AggregateException(failures);
    }

    private static TimeSpan Remaining(long deadline)
    {
        long milliseconds = deadline - Environment.TickCount64;
        return milliseconds <= 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(milliseconds);
    }

    private static void AddFailure(ref List<Exception>? failures, Exception failure)
    {
        failures ??= new List<Exception>();
        failures.Add(failure);
    }

    private void CloseSessionResources()
    {
        if (Interlocked.Exchange(ref _sessionResourcesClosed, 1) != 0)
            return;
        CloseConsole();
        _inputWrite.Dispose();
        _conptyInputRead?.Dispose();
        _conptyOutputWrite?.Dispose();
        _outputRead.Dispose();
    }

    private void CloseWatcherResources()
    {
        if (Interlocked.Exchange(ref _watcherCleanupState, 3) == 3)
            return;
        if (_threadHandle != IntPtr.Zero)
            CloseHandle(_threadHandle);
        if (_processHandle != IntPtr.Zero)
            CloseHandle(_processHandle);
        _exitEvent.Dispose();
        _disposeRequested.Dispose();
        _logger.WinDisposeHandlesClosed(SessionId);
        _testHooks?.WatcherResourcesClosed?.Invoke();
    }

    private void CloseHandle(IntPtr handle)
    {
        if (_testHooks?.CloseHandle is { } closeHandle)
        {
            closeHandle(handle);
            return;
        }
        if (!ConPtyNative.CloseHandle(handle))
        {
            int error = Marshal.GetLastPInvokeError();
            throw new PtyIoException(
                $"failed to close Windows PTY session {SessionId} handle (error {error}).",
                error);
        }
    }
}

internal sealed class WindowsPtySessionTestHooks
{
    public required Func<int> WaitForExit { get; init; }
    public Action? TerminateProcess { get; init; }
    public Action<IntPtr>? CloseHandle { get; init; }
    public Action? ExitSignalSet { get; init; }
    public Action? WatcherResourcesClosed { get; init; }
    public TimeSpan DisposeTimeout { get; init; } = TimeSpan.FromMilliseconds(3000);
}
