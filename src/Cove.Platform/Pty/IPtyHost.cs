namespace Cove.Platform.Pty;

public interface IPtyHost
{
    bool IsSupported { get; }
    IPtySession Spawn(PtySpawnRequest request);

    bool TryExportSession(IPtySession session, out int masterFd, out int pid)
    {
        masterFd = -1;
        pid = -1;
        return false;
    }

    IPtySession AdoptSession(int masterFd, int pid)
        => throw new PlatformNotSupportedException("session adoption is not supported by this pty host");
}
