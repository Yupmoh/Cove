using System.Diagnostics;
using Cove.Protocol;

namespace Cove.Engine.Daemon;

public sealed class SpawnLock : IDisposable
{
    private readonly FileStream? _stream;

    private SpawnLock(FileStream? stream) => _stream = stream;

    public static SpawnLock Acquire(string path)
    {
        var sw = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                if (!OperatingSystem.IsWindows())
                {
                    int fd = (int)fs.SafeFileHandle.DangerousGetHandle();
                    int rc = NativeFlock.Flock(fd, NativeFlock.LockEx | NativeFlock.LockNb);
                    if (rc != 0)
                    {
                        fs.Dispose();
                        throw new IOException("spawn lock busy");
                    }
                }
                return new SpawnLock(fs);
            }
            catch (IOException)
            {
                if (sw.ElapsedMilliseconds >= ProtocolConstants.ReadinessTimeoutMs)
                    return new SpawnLock(null);
                Thread.Sleep(ProtocolConstants.SpawnPollMs);
            }
        }
    }

    public void Dispose() => _stream?.Dispose();
}
