using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace Cove.Platform.Pty.Windows;

public sealed class WindowsPtySession : IPtySession
{
    private const int ConsoleFlushGraceMilliseconds = 40;

    private readonly ILogger _logger;
    private readonly SafeFileHandle _outputRead;
    private readonly SafeFileHandle _inputWrite;
    private readonly IntPtr _pseudoConsole;
    private readonly IntPtr _processHandle;
    private readonly IntPtr _threadHandle;
    private readonly int _processId;
    private readonly ManualResetEventSlim _exitEvent = new(false);
    private readonly Thread _exitWatcher;

    private int _hasExited;
    private int _exitCode = -1;
    private int _consoleClosed;
    private int _killed;
    private int _handlesClosed;
    private int _disposed;

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
    }

    public long SessionId { get; }
    public bool HasExited => Volatile.Read(ref _hasExited) != 0;
    public int ExitCode => HasExited ? _exitCode : -1;

    public int Read(Span<byte> buffer)
    {
        if (buffer.IsEmpty)
            return 0;
        if (ConPtyNative.ReadFile(_outputRead, buffer, buffer.Length, out int read, IntPtr.Zero))
            return read;
        int error = Marshal.GetLastPInvokeError();
        if (error is ConPtyNative.ErrorBrokenPipe
            or ConPtyNative.ErrorHandleEof
            or ConPtyNative.ErrorOperationAborted
            or ConPtyNative.ErrorNoData)
            return 0;
        throw new PtyIoException($"conpty read failed (session {SessionId}, error {error}).", error);
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return;
        if (data.Length > PtyConstants.MaxWriteBytes)
            throw new ArgumentException($"write exceeds {PtyConstants.MaxWriteBytes} bytes.", nameof(data));
        int offset = 0;
        while (offset < data.Length)
        {
            var slice = data.Slice(offset);
            if (!ConPtyNative.WriteFile(_inputWrite, slice, slice.Length, out int written, IntPtr.Zero))
            {
                int error = Marshal.GetLastPInvokeError();
                throw new PtyIoException($"conpty write failed (session {SessionId}, error {error}).", error);
            }
            if (written <= 0)
            {
                _logger.LogWarning("conpty write made no progress (session {Id}, offset {Offset}).", SessionId, offset);
                break;
            }
            offset += written;
        }
    }

    public void Resize(int cols, int rows)
    {
        ushort c = (ushort)Math.Clamp(cols, PtyConstants.MinCols, PtyConstants.MaxCols);
        ushort r = (ushort)Math.Clamp(rows, PtyConstants.MinRows, PtyConstants.MaxRows);
        if (c != cols || r != rows)
            _logger.LogWarning("conpty resize clamped ({Cols}x{Rows} -> {C}x{R}) session {Id}.", cols, rows, c, r, SessionId);
        if (Volatile.Read(ref _consoleClosed) != 0)
            return;
        var size = new ConPtyNative.Coord { X = (short)c, Y = (short)r };
        int hr = ConPtyNative.ResizePseudoConsole(_pseudoConsole, size);
        if (hr != 0)
            _logger.LogWarning("conpty resize failed (session {Id}, hr 0x{Hr:X8}).", SessionId, hr);
    }

    public void Kill()
    {
        if (Interlocked.Exchange(ref _killed, 1) != 0)
            return;
        if (HasExited)
            return;
        if (!ConPtyNative.TerminateProcess(_processHandle, 1))
        {
            int error = Marshal.GetLastPInvokeError();
            _logger.LogWarning("conpty kill failed (session {Id}, error {Error}).", SessionId, error);
        }
    }

    public bool Signal(int signum)
    {
        if (signum is PtyConstants.SigKill or PtyConstants.SigTerm)
        {
            Kill();
            return true;
        }
        _logger.LogWarning("conpty signal {Sig} unsupported on Windows (session {Id}).", signum, SessionId);
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
            _exitCode = -1;
        Volatile.Write(ref _hasExited, 1);
        _exitEvent.Set();
        if (ConsoleFlushGraceMilliseconds > 0)
            Thread.Sleep(ConsoleFlushGraceMilliseconds);
        CloseConsole();
    }

    private void CloseConsole()
    {
        if (Interlocked.Exchange(ref _consoleClosed, 1) != 0)
            return;
        ConPtyNative.ClosePseudoConsole(_pseudoConsole);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

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
        }

        _outputRead.Dispose();
        _inputWrite.Dispose();
    }
}
