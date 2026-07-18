using System.Globalization;
using System.Text;
using Cove.Platform.Ipc;

namespace Cove.Engine.Daemon;

public sealed class SingleInstanceGuard : IDisposable
{
    private readonly FileStream _pidStream;
    private readonly FileLock? _fileLock;
    private int _disposed;

    private SingleInstanceGuard(FileStream pidStream, FileLock? fileLock)
    {
        _pidStream = pidStream;
        _fileLock = fileLock;
    }

    public static SingleInstanceGuard? TryAcquire(string pidFilePath)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                FileStream win = new FileStream(pidFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                return new SingleInstanceGuard(win, null);
            }
            catch (IOException)
            {
                return null;
            }
        }
        FileStream fs = new FileStream(pidFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        try { File.SetUnixFileMode(pidFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
        catch { }
        FileLock? fileLock = FileLock.TryAcquire(fs);
        if (fileLock is null)
        {
            fs.Dispose();
            return null;
        }
        return new SingleInstanceGuard(fs, fileLock);
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
        _fileLock?.Dispose();
        _pidStream.Dispose();
    }
}
