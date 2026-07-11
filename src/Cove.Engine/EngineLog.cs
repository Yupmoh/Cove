using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine;

public static partial class EngineLog
{
    [ZLoggerMessage(LogLevel.Information, "daemon started pid={pid} channel={channel}")]
    public static partial void DaemonStarted(this ILogger logger, int pid, string channel);

    [ZLoggerMessage(LogLevel.Information, "daemon stopping channel={channel}")]
    public static partial void DaemonStopping(this ILogger logger, string channel);

    [ZLoggerMessage(LogLevel.Information, "session opened nook={nookId} command={command}")]
    public static partial void SessionOpened(this ILogger logger, string nookId, string command);

    [ZLoggerMessage(LogLevel.Information, "session closed nook={nookId} exitCode={exitCode}")]
    public static partial void SessionClosed(this ILogger logger, string nookId, int exitCode);

    [ZLoggerMessage(LogLevel.Debug, "session activity nook={nookId} bytes={bytes}")]
    public static partial void SessionActivity(this ILogger logger, string nookId, int bytes);
}
