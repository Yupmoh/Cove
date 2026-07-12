using System.Globalization;
using System.Text;

namespace Cove.Engine.Daemon;

public sealed class SingleInstanceGuard : IDisposable
{
    private readonly FileStream _pidStream;
    private int _disposed;

    private SingleInstanceGuard(FileStream pidStream) => _pidStream = pidStream;

    public static SingleInstanceGuard? TryAcquire(string pidFilePath)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                FileStream win = new FileStream(pidFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                return new SingleInstanceGuard(win);
            }
            catch (IOException)
            {
                return null;
            }
        }
        FileStream fs = new FileStream(pidFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        try { File.SetUnixFileMode(pidFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
        catch { }
        int fd = (int)fs.SafeFileHandle.DangerousGetHandle();
        int rc = NativeFlock.Flock(fd, NativeFlock.LockEx | NativeFlock.LockNb);
        if (rc != 0)
        {
            fs.Dispose();
            return null;
        }
        return new SingleInstanceGuard(fs);
    }

    public void WritePid(int pid)
    {
        _pidStream.SetLength(0);
        _pidStream.Seek(0, SeekOrigin.Begin);
        byte[] bytes = Encoding.ASCII.GetBytes(pid.ToString(CultureInfo.InvariantCulture) + "\n");
        _pidStream.Write(bytes, 0, bytes.Length);
        _pidStream.Flush(true);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _pidStream.Dispose();
    }
}
