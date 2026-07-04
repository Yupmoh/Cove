using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine;

public static partial class EngineLog
{
    [ZLoggerMessage(LogLevel.Information, "daemon started pid={pid} channel={channel}")]
    public static partial void DaemonStarted(this ILogger logger, int pid, string channel);

    [ZLoggerMessage(LogLevel.Information, "daemon stopping channel={channel}")]
    public static partial void DaemonStopping(this ILogger logger, string channel);

    [ZLoggerMessage(LogLevel.Information, "session opened pane={paneId} command={command}")]
    public static partial void SessionOpened(this ILogger logger, string paneId, string command);

    [ZLoggerMessage(LogLevel.Information, "session closed pane={paneId} exitCode={exitCode}")]
    public static partial void SessionClosed(this ILogger logger, string paneId, int exitCode);

    [ZLoggerMessage(LogLevel.Debug, "session activity pane={paneId} bytes={bytes}")]
    public static partial void SessionActivity(this ILogger logger, string paneId, int bytes);
}
