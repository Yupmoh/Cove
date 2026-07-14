using Microsoft.Extensions.Logging;
using Cove.Platform;

namespace Cove.Cli;

internal static class CliChannel
{
    internal static CoveChannel Resolve(string[] args, ILogger? logger = null)
        => Resolve(args, System.Environment.GetEnvironmentVariable("COVE_CHANNEL"), logger);

    internal static CoveChannel Resolve(string[] args, string? env, ILogger? logger)
    {
        var flag = FlagValue(args, "--channel");
        if (!string.IsNullOrWhiteSpace(flag))
        {
            if (TryParse(flag, out var flagChannel))
                return flagChannel;
            logger?.InvalidChannel(flag, "--channel");
            return CoveChannel.Stable;
        }
        if (!string.IsNullOrWhiteSpace(env))
        {
            if (TryParse(env, out var envChannel))
                return envChannel;
            logger?.InvalidChannel(env, "COVE_CHANNEL");
            return CoveChannel.Stable;
        }
        return CoveChannel.Stable;
    }

    internal static bool TryParse(string? value, out CoveChannel channel)
    {
        switch (value?.ToLowerInvariant())
        {
            case "stable":
                channel = CoveChannel.Stable;
                return true;
            case "beta":
                channel = CoveChannel.Beta;
                return true;
            case "dev":
                channel = CoveChannel.Dev;
                return true;
            default:
                channel = CoveChannel.Stable;
                return false;
        }
    }

    private static string? FlagValue(string[] args, string flag)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == flag && i + 1 < args.Length)
                return args[i + 1];
            if (args[i].StartsWith(flag + "=", System.StringComparison.Ordinal))
                return args[i][(flag.Length + 1)..];
        }
        return null;
    }
}
