using System.Runtime.InteropServices;

namespace Cove.Persistence;

internal static partial class NativePosix
{
    private const int O_RDONLY = 0;

    [LibraryImport("libc", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int open(string path, int flags);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int fsync(int fd);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int close(int fd);

    public static void FsyncDir(string dir)
    {
        int fd = open(dir, O_RDONLY);
        if (fd < 0) return;
        try { fsync(fd); }
        finally { close(fd); }
    }
}
