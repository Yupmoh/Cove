using System.Runtime.InteropServices;

namespace Cove.Platform.Terminal;

public sealed partial class InterruptibleConsoleInputStream : Stream
{
    private const int StandardInputHandle = -10;
    private const short PollInput = 0x0001;
    private const int InterruptedSystemCall = 4;
    private const uint WaitObject0 = 0;
    private const uint WaitTimeout = 258;
    private const uint WaitFailed = uint.MaxValue;
    private readonly Stream _input = Console.OpenStandardInput();
    private readonly PeriodicTimer _readinessTimer = new(TimeSpan.FromMilliseconds(10));
    private readonly nint _windowsHandle =
        OperatingSystem.IsWindows() ? NativeMethods.GetStdHandle(StandardInputHandle) : 0;

    public override bool CanRead => _input.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (InputReady())
                return _input.Read(buffer.Span);
            if (!await _readinessTimer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                return 0;
        }
    }

    public override int Read(Span<byte> buffer) => _input.Read(buffer);

    public override int Read(byte[] buffer, int offset, int count) =>
        _input.Read(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _readinessTimer.Dispose();
            _input.Dispose();
        }
        base.Dispose(disposing);
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private bool InputReady()
    {
        if (OperatingSystem.IsWindows())
        {
            if (!Console.IsInputRedirected)
                return Console.KeyAvailable;
            var result = NativeMethods.WaitForSingleObject(_windowsHandle, 0);
            return result switch
            {
                WaitObject0 => true,
                WaitTimeout => false,
                WaitFailed => throw new IOException(
                    $"standard input wait failed ({Marshal.GetLastPInvokeError()})"),
                _ => false
            };
        }

        var descriptor = new PollDescriptor
        {
            FileDescriptor = 0,
            Events = PollInput
        };
        var ready = NativeMethods.Poll(ref descriptor, 1, 0);
        if (ready >= 0)
            return ready > 0;
        var error = Marshal.GetLastPInvokeError();
        if (error == InterruptedSystemCall)
            return false;
        throw new IOException($"standard input poll failed ({error})");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PollDescriptor
    {
        public int FileDescriptor;
        public short Events;
        public short ReturnedEvents;
    }

    private static partial class NativeMethods
    {
        [LibraryImport("libc", EntryPoint = "poll", SetLastError = true)]
        internal static partial int Poll(
            ref PollDescriptor descriptors,
            uint count,
            int timeoutMilliseconds);

        [LibraryImport("kernel32.dll", EntryPoint = "GetStdHandle", SetLastError = true)]
        internal static partial nint GetStdHandle(int standardHandle);

        [LibraryImport("kernel32.dll", EntryPoint = "WaitForSingleObject", SetLastError = true)]
        internal static partial uint WaitForSingleObject(nint handle, uint milliseconds);
    }
}
