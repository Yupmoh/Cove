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

    [ZLoggerMessage(LogLevel.Warning, "directory listing rejected path={path} reason={reason}")]
    public static partial void DirectoryListingRejected(this ILogger logger, string path, string reason);

    [ZLoggerMessage(LogLevel.Warning, "git summary rejected path={path} reason={reason}")]
    public static partial void GitSummaryRejected(this ILogger logger, string path, string reason);

    [ZLoggerMessage(LogLevel.Warning, "feedback persistence failed slug={slug} error={error}")]
    public static partial void FeedbackPersistenceFailed(this ILogger logger, string slug, string error);

    [ZLoggerMessage(LogLevel.Warning, "performance result persistence failed root={root} error={error}")]
    public static partial void PerformanceResultPersistenceFailed(this ILogger logger, string root, string error);

    [ZLoggerMessage(LogLevel.Debug, "dictation model provisioning cancelled reason={reason}")]
    public static partial void DictationModelProvisioningCancelled(this ILogger logger, string reason);

    [ZLoggerMessage(LogLevel.Warning, "dictation model provisioning failed error={error}")]
    public static partial void DictationModelProvisioningFailed(this ILogger logger, string error);
}
