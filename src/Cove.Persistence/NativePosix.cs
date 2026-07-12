using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

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

    public static void FsyncDir(string dir, ILogger? logger = null)
    {
        int fd = open(dir, O_RDONLY);
        if (fd < 0)
        {
            logger?.FsyncDirOpenFailed(dir, Marshal.GetLastPInvokeError());
            return;
        }
        try
        {
            if (fsync(fd) != 0)
                logger?.FsyncDirFailed(dir, Marshal.GetLastPInvokeError());
        }
        finally { close(fd); }
    }
}
