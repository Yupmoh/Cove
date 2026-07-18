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

    [ZLoggerMessage(3303, LogLevel.Error, "control token write failed path={path} error={error}")]
    public static partial void ControlTokenWriteFailed(this ILogger logger, string path, string error);

    [ZLoggerMessage(3304, LogLevel.Warning, "gui hello rejected missing or invalid control token clientKind={clientKind}")]
    public static partial void GuiControlAuthRejected(this ILogger logger, string clientKind);

    [ZLoggerMessage(3305, LogLevel.Warning, "nook hello rejected invalid token nookId={nookId}")]
    public static partial void NookAuthRejected(this ILogger logger, string nookId);

    [ZLoggerMessage(3306, LogLevel.Warning, "nook hello claim for unknown nook treated as anonymous nookId={nookId}")]
    public static partial void NookAuthUnknown(this ILogger logger, string nookId);

    [ZLoggerMessage(3307, LogLevel.Information, "nook hello bound without token legacy session nookId={nookId}")]
    public static partial void NookAuthLegacyBound(this ILogger logger, string nookId);

    [ZLoggerMessage(3308, LogLevel.Warning, "caller claim overridden by connection principal claimed={claimed} principal={principal} uri={uri}")]
    public static partial void CallerClaimOverridden(this ILogger logger, string claimed, string principal, string uri);

    [ZLoggerMessage(3309, LogLevel.Warning, "anonymous caller claim of tokened nook stripped claimed={claimed} uri={uri}")]
    public static partial void CallerClaimStripped(this ILogger logger, string claimed, string uri);

    [ZLoggerMessage(3310, LogLevel.Warning, "scope mutation denied for non-gui connection principal={principal}")]
    public static partial void ScopeMutationDenied(this ILogger logger, string principal);
}
