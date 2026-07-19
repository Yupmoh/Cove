using Cove.Platform;
using Microsoft.Extensions.Logging;

namespace Cove.Gui;

public static class GuiLogging
{
    public static ILoggerFactory CreateFactory()
    {
        var channel = ResolveChannelName();
        var dataDir = CoveDataDir.Resolve(ParseChannel(channel));
        var minimumLevel = ParseLevel(Environment.GetEnvironmentVariable("COVE_LOG_LEVEL"));
        return CoveLog.CreateNamedFileLoggerFactory(dataDir.LogsDir, "gui", minimumLevel);
    }

    public static string ResolveChannelName()
    {
        var channel = Environment.GetEnvironmentVariable("COVE_CHANNEL");
        return string.IsNullOrWhiteSpace(channel) ? "dev" : channel.Trim().ToLowerInvariant();
    }

    public static CoveChannel ParseChannel(string channel) => channel switch
    {
        "beta" => CoveChannel.Beta,
        "stable" => CoveChannel.Stable,
        _ => CoveChannel.Dev,
    };

    public static string EndpointFor(string channel)
        => CoveDataDir.Resolve(ParseChannel(channel)).SocketPath;

    private static LogLevel ParseLevel(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "trace" => LogLevel.Trace,
        "debug" => LogLevel.Debug,
        "warn" => LogLevel.Warning,
        "warning" => LogLevel.Warning,
        "error" => LogLevel.Error,
        _ => LogLevel.Information,
    };
}
