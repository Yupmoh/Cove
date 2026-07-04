using Cove.Platform;

namespace Cove.Engine.Daemon;

public sealed class DaemonPaths
{
    public CoveDataDir DataDir { get; }
    public string Channel { get; }
    public string SocketPath => DataDir.SocketPath;
    public string PidFilePath { get; }
    public string SpawnLockPath { get; }
    public string DaemonLogPath { get; }

    public DaemonPaths(CoveDataDir dataDir)
    {
        DataDir = dataDir;
        Channel = Path.GetFileNameWithoutExtension(dataDir.SocketPath);
        PidFilePath = Path.Combine(dataDir.IpcDir, Channel + ".pid");
        SpawnLockPath = Path.Combine(dataDir.IpcDir, Channel + ".spawn.lock");
        DaemonLogPath = Path.Combine(dataDir.LogsDir, Channel + "-daemon.log");
    }
}
