using System;
using System.Runtime.InteropServices;

namespace Cove.Platform.Pty.Unix;

internal static partial class CovePtyNative
{
    [LibraryImport(PtyConstants.NativeLibrary, EntryPoint = "cove_pty_abi_version")]
    internal static partial int AbiVersion();

    [LibraryImport(PtyConstants.NativeLibrary, EntryPoint = "cove_pty_spawn", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int Spawn(string path, IntPtr argv, IntPtr envp, string? cwd, ushort cols, ushort rows, out int masterFd, out int pid);

    [LibraryImport(PtyConstants.NativeLibrary, EntryPoint = "cove_pty_read")]
    internal static partial nint Read(int fd, Span<byte> buffer, int length);

    [LibraryImport(PtyConstants.NativeLibrary, EntryPoint = "cove_pty_write")]
    internal static partial nint Write(int fd, ReadOnlySpan<byte> buffer, int length);

    [LibraryImport(PtyConstants.NativeLibrary, EntryPoint = "cove_pty_resize")]
    internal static partial int Resize(int fd, ushort cols, ushort rows);

    [LibraryImport(PtyConstants.NativeLibrary, EntryPoint = "cove_pty_kill")]
    internal static partial int Kill(int pid, int sig);

    [LibraryImport(PtyConstants.NativeLibrary, EntryPoint = "cove_pty_reap")]
    internal static partial int Reap(int pid);

    [LibraryImport(PtyConstants.NativeLibrary, EntryPoint = "cove_pty_close")]
    internal static partial void Close(int fd);

    [LibraryImport(PtyConstants.NativeLibrary, EntryPoint = "cove_pty_socketpair")]
    internal static partial int SocketPair(out int a, out int b);

    [LibraryImport(PtyConstants.NativeLibrary, EntryPoint = "cove_pty_send_with_fd")]
    internal static partial nint SendWithFd(int sock, ReadOnlySpan<byte> buffer, int length, int fd);

    [LibraryImport(PtyConstants.NativeLibrary, EntryPoint = "cove_pty_recv_with_fd")]
    internal static partial nint RecvWithFd(int sock, Span<byte> buffer, int length, out int fd);

    [LibraryImport(PtyConstants.NativeLibrary, EntryPoint = "cove_pty_exitwatch_new")]
    internal static partial nint ExitWatchNew();

    [LibraryImport(PtyConstants.NativeLibrary, EntryPoint = "cove_pty_exitwatch_add")]
    internal static partial int ExitWatchAdd(nint watch, int pid, long token);

    [LibraryImport(PtyConstants.NativeLibrary, EntryPoint = "cove_pty_exitwatch_remove")]
    internal static partial int ExitWatchRemove(nint watch, long token);

    [LibraryImport(PtyConstants.NativeLibrary, EntryPoint = "cove_pty_exitwatch_next")]
    internal static partial long ExitWatchNext(nint watch, out int status);

    [LibraryImport(PtyConstants.NativeLibrary, EntryPoint = "cove_pty_exitwatch_wait_reader_entered")]
    internal static partial int ExitWatchWaitReaderEntered(nint watch);

    [LibraryImport(PtyConstants.NativeLibrary, EntryPoint = "cove_pty_exitwatch_free")]
    internal static partial void ExitWatchFree(nint watch);

    [LibraryImport(PtyConstants.NativeLibrary, EntryPoint = "cove_pty_dup")]
    internal static partial int Dup(int fd);

    [LibraryImport(PtyConstants.NativeLibrary, EntryPoint = "cove_pty_poll_readable")]
    internal static partial int PollReadable(int fd, int timeoutMs);
}
