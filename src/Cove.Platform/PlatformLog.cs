using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Platform;

internal static partial class PlatformLog
{
    [ZLoggerMessage(LogLevel.Trace, "control endpoint resolved kind={kind} channel={channel} address={address}", EventId = 2000)]
    public static partial void EndpointResolved(this ILogger logger, string kind, string channel, string address);

    [ZLoggerMessage(LogLevel.Trace, "control endpoint bind begin transport={transport} address={address}", EventId = 2001)]
    public static partial void EndpointBindBegin(this ILogger logger, string transport, string address);

    [ZLoggerMessage(LogLevel.Information, "control endpoint bound transport={transport} address={address}", EventId = 2002)]
    public static partial void EndpointBound(this ILogger logger, string transport, string address);

    [ZLoggerMessage(LogLevel.Error, "control endpoint bind failed transport={transport} address={address} error={error}", EventId = 2003)]
    public static partial void EndpointBindFailed(this ILogger logger, string transport, string address, string error);

    [ZLoggerMessage(LogLevel.Trace, "control endpoint connect begin transport={transport} address={address} timeoutMs={timeoutMs}", EventId = 2004)]
    public static partial void EndpointConnectBegin(this ILogger logger, string transport, string address, int timeoutMs);

    [ZLoggerMessage(LogLevel.Trace, "control endpoint connected transport={transport} address={address}", EventId = 2005)]
    public static partial void EndpointConnected(this ILogger logger, string transport, string address);

    [ZLoggerMessage(LogLevel.Error, "control endpoint connect failed transport={transport} address={address} error={error}", EventId = 2006)]
    public static partial void EndpointConnectFailed(this ILogger logger, string transport, string address, string error);

    [ZLoggerMessage(LogLevel.Trace, "control endpoint probe begin transport={transport} address={address} timeoutMs={timeoutMs}", EventId = 2007)]
    public static partial void EndpointProbeBegin(this ILogger logger, string transport, string address, int timeoutMs);

    [ZLoggerMessage(LogLevel.Trace, "control endpoint probe reachable transport={transport} address={address}", EventId = 2008)]
    public static partial void EndpointProbeReachable(this ILogger logger, string transport, string address);

    [ZLoggerMessage(LogLevel.Warning, "control endpoint probe unreachable transport={transport} address={address} error={error}", EventId = 2009)]
    public static partial void EndpointProbeUnreachable(this ILogger logger, string transport, string address, string error);

    [ZLoggerMessage(LogLevel.Trace, "control endpoint accept begin transport={transport} address={address}", EventId = 2010)]
    public static partial void EndpointAcceptBegin(this ILogger logger, string transport, string address);

    [ZLoggerMessage(LogLevel.Trace, "control endpoint accepted connection transport={transport} address={address}", EventId = 2011)]
    public static partial void EndpointAccepted(this ILogger logger, string transport, string address);

    [ZLoggerMessage(LogLevel.Warning, "control endpoint accept io error, recreating pipe instance address={address} error={error}", EventId = 2012)]
    public static partial void EndpointAcceptRetry(this ILogger logger, string address, string error);

    [ZLoggerMessage(LogLevel.Trace, "control endpoint server pipe instance created address={address} firstInstance={firstInstance}", EventId = 2013)]
    public static partial void EndpointServerInstanceCreated(this ILogger logger, string address, bool firstInstance);

    [ZLoggerMessage(LogLevel.Trace, "cove tree ensure begin root={root}", EventId = 2050)]
    public static partial void CoveTreeEnsureBegin(this ILogger logger, string root);

    [ZLoggerMessage(LogLevel.Trace, "cove tree directory created path={path}", EventId = 2051)]
    public static partial void CoveTreeDirCreated(this ILogger logger, string path);

    [ZLoggerMessage(LogLevel.Trace, "cove tree gitignore written path={path}", EventId = 2052)]
    public static partial void CoveTreeGitIgnoreWritten(this ILogger logger, string path);

    [ZLoggerMessage(LogLevel.Trace, "cove tree meta written path={path} schemaVersion={schemaVersion}", EventId = 2053)]
    public static partial void CoveTreeMetaWritten(this ILogger logger, string path, int schemaVersion);

    [ZLoggerMessage(LogLevel.Trace, "cove data dir resolved channel={channel} root={root} source={source}", EventId = 2054)]
    public static partial void CoveDataDirResolved(this ILogger logger, string channel, string root, string source);

    [ZLoggerMessage(LogLevel.Trace, "login shell path probe begin shell={shell}", EventId = 2070)]
    public static partial void LoginShellProbeBegin(this ILogger logger, string shell);

    [ZLoggerMessage(LogLevel.Trace, "login shell path resolved length={length}", EventId = 2071)]
    public static partial void LoginShellResolved(this ILogger logger, int length);

    [ZLoggerMessage(LogLevel.Warning, "login shell path probe empty output, falling back to process path shell={shell}", EventId = 2072)]
    public static partial void LoginShellProbeEmpty(this ILogger logger, string shell);

    [ZLoggerMessage(LogLevel.Warning, "login shell path probe timed out, killing process and falling back to process path shell={shell}", EventId = 2073)]
    public static partial void LoginShellProbeTimeout(this ILogger logger, string shell);

    [ZLoggerMessage(LogLevel.Warning, "login shell path probe kill failed error={error}", EventId = 2074)]
    public static partial void LoginShellKillFailed(this ILogger logger, string error);

    [ZLoggerMessage(LogLevel.Warning, "login shell path probe failed, falling back to process path error={error}", EventId = 2075)]
    public static partial void LoginShellProbeFailed(this ILogger logger, string error);

    [ZLoggerMessage(LogLevel.Warning, "file durability directory open failed path={path} errno={errno}", EventId = 2080)]
    public static partial void FileDurabilityDirectoryOpenFailed(this ILogger logger, string path, int errno);

    [ZLoggerMessage(LogLevel.Warning, "file durability directory flush failed path={path} errno={errno}", EventId = 2081)]
    public static partial void FileDurabilityDirectoryFlushFailed(this ILogger logger, string path, int errno);

    [ZLoggerMessage(LogLevel.Warning, "file durability directory close failed path={path} errno={errno}", EventId = 2082)]
    public static partial void FileDurabilityDirectoryCloseFailed(this ILogger logger, string path, int errno);
}
