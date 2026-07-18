using System.Runtime.InteropServices;

namespace Cove.Platform.Terminal;

public abstract partial class RawModeScope : IDisposable, IAsyncDisposable
{
    private int _disposed;

    protected RawModeScope()
    {
    }

    public static RawModeScope? TryEnter(int fileDescriptor = 0)
    {
        if (OperatingSystem.IsLinux())
            return TryEnterLinux(fileDescriptor);

        if (OperatingSystem.IsMacOS())
            return TryEnterMacOS(fileDescriptor);

        return null;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        Restore();
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    protected abstract void Restore();

    private static RawModeScope? TryEnterLinux(int fileDescriptor)
    {
        LinuxTermios saved = default;
        if (NativeMethods.tcgetattr_linux(fileDescriptor, ref saved) != 0)
            return null;

        LinuxTermios raw = saved;
        NativeMethods.cfmakeraw_linux(ref raw);
        if (NativeMethods.tcsetattr_linux(fileDescriptor, 0, ref raw) != 0)
            return null;

        return new LinuxRawModeScope(fileDescriptor, saved);
    }

    private static RawModeScope? TryEnterMacOS(int fileDescriptor)
    {
        MacTermios saved = default;
        if (NativeMethods.tcgetattr_macos(fileDescriptor, ref saved) != 0)
            return null;

        MacTermios raw = saved;
        NativeMethods.cfmakeraw_macos(ref raw);
        if (NativeMethods.tcsetattr_macos(fileDescriptor, 0, ref raw) != 0)
            return null;

        return new MacRawModeScope(fileDescriptor, saved);
    }

    private sealed class LinuxRawModeScope : RawModeScope
    {
        private readonly int _fileDescriptor;
        private LinuxTermios _saved;

        public LinuxRawModeScope(int fileDescriptor, LinuxTermios saved)
        {
            _fileDescriptor = fileDescriptor;
            _saved = saved;
        }

        protected override void Restore()
        {
            NativeMethods.tcsetattr_linux(_fileDescriptor, 0, ref _saved);
        }
    }

    private sealed class MacRawModeScope : RawModeScope
    {
        private readonly int _fileDescriptor;
        private MacTermios _saved;

        public MacRawModeScope(int fileDescriptor, MacTermios saved)
        {
            _fileDescriptor = fileDescriptor;
            _saved = saved;
        }

        protected override void Restore()
        {
            NativeMethods.tcsetattr_macos(_fileDescriptor, 0, ref _saved);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct LinuxTermios
    {
        public uint InputFlags;
        public uint OutputFlags;
        public uint ControlFlags;
        public uint LocalFlags;
        public byte LineDiscipline;
        public fixed byte ControlCharacters[32];
        public uint InputSpeed;
        public uint OutputSpeed;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct MacTermios
    {
        public nuint InputFlags;
        public nuint OutputFlags;
        public nuint ControlFlags;
        public nuint LocalFlags;
        public fixed byte ControlCharacters[20];
        public nuint InputSpeed;
        public nuint OutputSpeed;
    }

    private static partial class NativeMethods
    {
        [LibraryImport("libc", EntryPoint = "tcgetattr", SetLastError = true)]
        internal static partial int tcgetattr_linux(int fileDescriptor, ref LinuxTermios termios);

        [LibraryImport("libc", EntryPoint = "tcsetattr", SetLastError = true)]
        internal static partial int tcsetattr_linux(int fileDescriptor, int actions, ref LinuxTermios termios);

        [LibraryImport("libc", EntryPoint = "cfmakeraw")]
        internal static partial void cfmakeraw_linux(ref LinuxTermios termios);

        [LibraryImport("libc", EntryPoint = "tcgetattr", SetLastError = true)]
        internal static partial int tcgetattr_macos(int fileDescriptor, ref MacTermios termios);

        [LibraryImport("libc", EntryPoint = "tcsetattr", SetLastError = true)]
        internal static partial int tcsetattr_macos(int fileDescriptor, int actions, ref MacTermios termios);

        [LibraryImport("libc", EntryPoint = "cfmakeraw")]
        internal static partial void cfmakeraw_macos(ref MacTermios termios);
    }
}
