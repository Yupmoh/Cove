using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cove.Platform.Ipc;

public static class ControlEndpointFactory
{
    public static IControlEndpoint FromSocketPath(string socketPath, ILogger? logger = null)
    {
        ILogger log = logger ?? NullLogger.Instance;
        string channel = Path.GetFileNameWithoutExtension(socketPath);
        if (OperatingSystem.IsWindows())
        {
            string pipeName = $"cove-{channel}-{StablePathHash(socketPath)}";
            var windows = new WindowsControlEndpoint(pipeName, log);
            log.EndpointResolved("named-pipe", channel, windows.Address);
            return windows;
        }
        var unix = new UnixControlEndpoint(socketPath, log);
        log.EndpointResolved("unix-socket", channel, unix.Address);
        return unix;
    }

    private static string StablePathHash(string path)
    {
        ulong hash = 14695981039346656037UL;
        foreach (char c in path)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }
        return hash.ToString("x16");
    }
}
