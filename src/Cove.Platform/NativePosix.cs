using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Cove.Platform;

internal static partial class NativePosix
{
    private const int ReadOnly = 0;

    [LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Open(string path, int flags);

    [LibraryImport("libc", EntryPoint = "fsync", SetLastError = true)]
    private static partial int Fsync(int fileDescriptor);

    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    private static partial int Close(int fileDescriptor);

    public static void FlushDirectory(string path, ILogger? logger)
    {
        var fileDescriptor = Open(path, ReadOnly);
        if (fileDescriptor < 0)
        {
            logger?.FileDurabilityDirectoryOpenFailed(path, Marshal.GetLastPInvokeError());
            return;
        }

        try
        {
            if (Fsync(fileDescriptor) != 0)
                logger?.FileDurabilityDirectoryFlushFailed(path, Marshal.GetLastPInvokeError());
        }
        finally
        {
            if (Close(fileDescriptor) != 0)
                logger?.FileDurabilityDirectoryCloseFailed(path, Marshal.GetLastPInvokeError());
        }
    }
}
