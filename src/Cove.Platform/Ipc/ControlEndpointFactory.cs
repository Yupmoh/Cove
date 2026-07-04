namespace Cove.Platform.Ipc;

public static class ControlEndpointFactory
{
    public static IControlEndpoint FromSocketPath(string socketPath)
    {
        string channel = Path.GetFileNameWithoutExtension(socketPath);
        if (OperatingSystem.IsWindows())
            return new WindowsControlEndpoint($"cove-{channel}-{StablePathHash(socketPath)}");
        return new UnixControlEndpoint(socketPath);
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
