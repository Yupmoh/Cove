using System.Diagnostics;
using Cove.Platform.Ipc;
using Cove.Protocol;

namespace Cove.Engine.Daemon;

public sealed class SpawnLock : IDisposable
{
    private readonly FileStream? _stream;
    private readonly FileLock? _fileLock;

    private SpawnLock(FileStream? stream, FileLock? fileLock)
    {
        _stream = stream;
        _fileLock = fileLock;
    }

    public static SpawnLock Acquire(string path)
    {
        var sw = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                FileLock? fileLock = null;
                if (!OperatingSystem.IsWindows())
                {
                    fileLock = FileLock.TryAcquire(fs);
                    if (fileLock is null)
                    {
                        fs.Dispose();
                        throw new IOException("spawn lock busy");
                    }
                }
                return new SpawnLock(fs, fileLock);
            }
            catch (IOException)
            {
                if (sw.ElapsedMilliseconds >= ProtocolConstants.ReadinessTimeoutMs)
                    return new SpawnLock(null, null);
                Thread.Sleep(ProtocolConstants.SpawnPollMs);
            }
        }
    }

    public void Dispose()
    {
        _fileLock?.Dispose();
        _stream?.Dispose();
    }
}
