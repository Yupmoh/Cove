using Cove.Engine.Daemon;
using Cove.Platform;
using Cove.Platform.Ipc;
using Cove.Tui.Attach;

namespace Cove.Tui;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--raw")
        {
            Console.Error.WriteLine("Cove attach --raw is not implemented.");
            return 1;
        }

        if (!TryParse(args, out var nookId, out var channel))
            return 2;

        var dataDirectory = CoveDataDir.Resolve(channel);
        var paths = new DaemonPaths(dataDirectory);
        var endpoint = ControlEndpointFactory.FromSocketPath(dataDirectory.SocketPath);
        return await AttachCompositor.RunAsync(
            paths,
            endpoint,
            nookId,
            "user:tui").ConfigureAwait(false);
    }

    private static bool TryParse(string[] args, out string nookId, out CoveChannel channel)
    {
        nookId = "";
        channel = CoveChannel.Stable;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--channel")
            {
                if (++i >= args.Length || !TryParseChannel(args[i], out channel))
                {
                    Console.Error.WriteLine("usage: Cove.Tui <nookId> [--channel stable|beta|dev]");
                    return false;
                }
                continue;
            }
            if (args[i].StartsWith("--", StringComparison.Ordinal) || nookId.Length != 0)
            {
                Console.Error.WriteLine("usage: Cove.Tui <nookId> [--channel stable|beta|dev]");
                return false;
            }
            nookId = args[i];
        }

        if (nookId.Length != 0)
            return true;
        Console.Error.WriteLine("usage: Cove.Tui <nookId> [--channel stable|beta|dev]");
        return false;
    }

    private static bool TryParseChannel(string value, out CoveChannel channel)
    {
        channel = value switch
        {
            "stable" => CoveChannel.Stable,
            "beta" => CoveChannel.Beta,
            "dev" => CoveChannel.Dev,
            _ => default
        };
        return value is "stable" or "beta" or "dev";
    }
}
