using System.Runtime.InteropServices;

namespace Cove.Engine.Daemon;

internal static partial class NativeFlock
{
    public const int LockEx = 2;
    public const int LockNb = 4;
    public const int LockUn = 8;

    [LibraryImport("libc", EntryPoint = "flock", SetLastError = true)]
    internal static partial int Flock(int fd, int operation);
}
