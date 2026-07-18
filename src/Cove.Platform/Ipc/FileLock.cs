using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace Cove.Platform.Ipc;

public sealed partial class FileLock : IDisposable
{
    private const int LockExclusive = 2;
    private const int LockNonBlocking = 4;
    private const int LockUnlock = 8;

    private readonly SafeFileHandle _handle;
    private readonly int _fileDescriptor;
    private int _disposed;

    private FileLock(SafeFileHandle handle, int fileDescriptor)
    {
        _handle = handle;
        _fileDescriptor = fileDescriptor;
    }

    public static FileLock? TryAcquire(FileStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("Advisory file locks are supported only on POSIX platforms.");

        SafeFileHandle handle = stream.SafeFileHandle;
        bool addedReference = false;
        try
        {
            handle.DangerousAddRef(ref addedReference);
            int fileDescriptor = checked((int)handle.DangerousGetHandle());
            if (Flock(fileDescriptor, LockExclusive | LockNonBlocking) != 0)
                return null;

            var fileLock = new FileLock(handle, fileDescriptor);
            addedReference = false;
            return fileLock;
        }
        finally
        {
            if (addedReference)
                handle.DangerousRelease();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        Flock(_fileDescriptor, LockUnlock);
        _handle.DangerousRelease();
        GC.SuppressFinalize(this);
    }

    [LibraryImport("libc", EntryPoint = "flock", SetLastError = true)]
    private static partial int Flock(int fileDescriptor, int operation);
}
