using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace Cove.Platform.Pty.Windows;

public sealed class WindowsPtySession : IPtySession
{
    private const int ConsoleRenderSettleMilliseconds = 250;

    private readonly ILogger _logger;
    private readonly SafeFileHandle _outputRead;
    private readonly SafeFileHandle _inputWrite;
    private readonly IntPtr _pseudoConsole;
    private readonly IntPtr _processHandle;
    private readonly IntPtr _threadHandle;
    private readonly int _processId;
    private readonly ManualResetEventSlim _exitEvent = new(false);
    private readonly ManualResetEventSlim _disposeRequested = new(false);
    private readonly Thread _exitWatcher;

    private int _hasExited;
    private int _exitCode = -1;
    private int _consoleClosed;
    private int _killed;
    private int _handlesClosed;
    private int _disposed;
    private int _firstReadLogged;

    internal WindowsPtySession(
        long sessionId,
        IntPtr pseudoConsole,
        SafeFileHandle outputRead,
        SafeFileHandle inputWrite,
        IntPtr processHandle,
        IntPtr threadHandle,
        int processId,
        ILogger logger)
    {
        SessionId = sessionId;
        _pseudoConsole = pseudoConsole;
        _outputRead = outputRead;
        _inputWrite = inputWrite;
        _processHandle = processHandle;
        _threadHandle = threadHandle;
        _processId = processId;
        _logger = logger;

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
        if (ConPtyNative.ReadFile(_outputRead, buffer, buffer.Length, out int read, IntPtr.Zero))
        {
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
        _exitEvent.Wait();
        return _exitCode;
    }

    private void WatchForExit()
    {
        ConPtyNative.WaitForSingleObject(_processHandle, ConPtyNative.Infinite);
        if (ConPtyNative.GetExitCodeProcess(_processHandle, out uint code))
            _exitCode = unchecked((int)code);
        else
        {
            _logger.WinGetExitCodeFailed(SessionId, Marshal.GetLastPInvokeError());
            _exitCode = -1;
        }
        Volatile.Write(ref _hasExited, 1);
        _exitEvent.Set();
        _logger.WinExitObserved(SessionId, _exitCode);
        _logger.SessionExited(SessionId, _exitCode);
        if (ConsoleRenderSettleMilliseconds > 0 && Volatile.Read(ref _disposed) == 0)
            _disposeRequested.Wait(ConsoleRenderSettleMilliseconds);
        CloseConsole();
    }

    private void CloseConsole()
    {
        if (Interlocked.Exchange(ref _consoleClosed, 1) != 0)
            return;
        _logger.WinPseudoConsoleClosed(SessionId);
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

        _exitEvent.Wait(TimeSpan.FromSeconds(3));
        CloseConsole();
        _exitWatcher.Join(TimeSpan.FromSeconds(3));

        if (Interlocked.Exchange(ref _handlesClosed, 1) == 0)
        {
            if (_threadHandle != IntPtr.Zero)
                ConPtyNative.CloseHandle(_threadHandle);
            if (_processHandle != IntPtr.Zero)
                ConPtyNative.CloseHandle(_processHandle);
            _logger.WinDisposeHandlesClosed(SessionId);
        }

        _outputRead.Dispose();
        _inputWrite.Dispose();
        _disposeRequested.Dispose();
    }
}
