using System.Globalization;
using System.Text;

namespace Cove.Engine.Daemon;

public static class DaemonLog
{
    private static readonly object Sync = new();

    public static void Write(DaemonPaths paths, string message)
    {
        try
        {
            string line = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " " + message + "\n";
            lock (Sync)
                File.AppendAllText(paths.DaemonLogPath, line, Encoding.UTF8);
        }
        catch
        {
        }
    }
}
