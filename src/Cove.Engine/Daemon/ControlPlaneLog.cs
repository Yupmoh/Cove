using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Daemon;

internal static partial class ControlPlaneLog
{
    [ZLoggerMessage(3300, LogLevel.Trace, "control dispatch uri={uri} durationMs={durationMs} ok={ok}")]
    public static partial void ControlDispatch(this ILogger logger, string uri, double durationMs, bool ok);

    [ZLoggerMessage(3301, LogLevel.Warning, "control dispatch failed uri={uri} code={code} message={message}")]
    public static partial void ControlDispatchFailed(this ILogger logger, string uri, string code, string message);

    [ZLoggerMessage(3302, LogLevel.Information, "engine log level resolved level={level} envValue={envValue}")]
    public static partial void LogLevelResolved(this ILogger logger, string level, string envValue);
}
