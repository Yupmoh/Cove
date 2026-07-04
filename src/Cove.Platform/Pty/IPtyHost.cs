namespace Cove.Platform.Pty;

public interface IPtyHost
{
    bool IsSupported { get; }
    IPtySession Spawn(PtySpawnRequest request);
}
