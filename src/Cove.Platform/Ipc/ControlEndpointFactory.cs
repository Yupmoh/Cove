namespace Cove.Platform.Ipc;

public static class ControlEndpointFactory
{
    public static IControlEndpoint FromSocketPath(string socketPath)
    {
        string channel = Path.GetFileNameWithoutExtension(socketPath);
        if (OperatingSystem.IsWindows())
            return new WindowsControlEndpoint($"cove-{channel}");
        return new UnixControlEndpoint(socketPath);
    }
}
