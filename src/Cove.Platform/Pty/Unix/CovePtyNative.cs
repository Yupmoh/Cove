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
}
