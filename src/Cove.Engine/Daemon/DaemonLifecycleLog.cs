using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Daemon;

internal static partial class DaemonLifecycleLog
{
    [ZLoggerMessage(3311, LogLevel.Warning, "daemon data directory already owned pid={pid}")]
    public static partial void DataDirectoryAlreadyOwned(this ILogger logger, string pid);

    [ZLoggerMessage(3312, LogLevel.Warning, "daemon channel already owned channel={channel}")]
    public static partial void DaemonChannelAlreadyOwned(this ILogger logger, string channel);

    [ZLoggerMessage(3313, LogLevel.Warning, "daemon control endpoint already owned channel={channel}")]
    public static partial void DaemonEndpointAlreadyOwned(this ILogger logger, string channel);

    [ZLoggerMessage(3314, LogLevel.Warning, "daemon control bind failed channel={channel} error={error}")]
    public static partial void DaemonControlBindFailed(this ILogger logger, string channel, string error);

    [ZLoggerMessage(3315, LogLevel.Warning, "daemon socket cleanup failed path={path} error={error}")]
    public static partial void DaemonSocketCleanupFailed(this ILogger logger, string path, string error);

    [ZLoggerMessage(3316, LogLevel.Warning, "daemon listener cleanup failed error={error}")]
    public static partial void DaemonListenerCleanupFailed(this ILogger logger, string error);

    [ZLoggerMessage(3317, LogLevel.Warning, "bay order persistence failed path={path} error={error}")]
    public static partial void BayOrderPersistenceFailed(this ILogger logger, string path, string error);

    [ZLoggerMessage(3318, LogLevel.Warning, "nook resize persistence skipped because nook has no layout bay nookId={nookId}")]
    public static partial void NookResizePersistenceSkipped(this ILogger logger, string nookId);

    [ZLoggerMessage(3319, LogLevel.Warning, "bay persistence skipped before coordinator attachment bayId={bayId}")]
    public static partial void BayPersistenceBeforeAttach(this ILogger logger, string bayId);

    [ZLoggerMessage(3320, LogLevel.Warning, "bay persistence skipped because bay is absent from layout bayId={bayId}")]
    public static partial void BayPersistenceMissingLayout(this ILogger logger, string bayId);

    [ZLoggerMessage(3321, LogLevel.Warning, "bay persistence failed error={error}")]
    public static partial void BayPersistenceFailed(this ILogger logger, string error);

    [ZLoggerMessage(3322, LogLevel.Warning, "scrollback persistence failed error={error}")]
    public static partial void ScrollbackPersistenceFailed(this ILogger logger, string error);

    [ZLoggerMessage(3323, LogLevel.Warning, "session persistence skipped before coordinator attachment nookId={nookId}")]
    public static partial void SessionPersistenceBeforeAttach(this ILogger logger, string nookId);

    [ZLoggerMessage(3324, LogLevel.Warning, "nook restoration respawn failed nookId={nookId} error={error}")]
    public static partial void NookRestorationRespawnFailed(this ILogger logger, string nookId, string error);

    [ZLoggerMessage(3325, LogLevel.Warning, "non-terminal task runs restored as interrupted count={count}")]
    public static partial void TaskRunsRestored(this ILogger logger, int count);

    [ZLoggerMessage(3326, LogLevel.Warning, "configured theme unavailable theme={theme}")]
    public static partial void ConfiguredThemeUnavailable(this ILogger logger, string theme);

    [ZLoggerMessage(3327, LogLevel.Warning, "configured theme unavailable, fallback activated theme={theme} fallback={fallback}")]
    public static partial void ConfiguredThemeFallback(this ILogger logger, string theme, string fallback);

    [ZLoggerMessage(3328, LogLevel.Warning, "bay startup adopted bayId={bayId} name={name} shores={shores} directory={directory}")]
    public static partial void BayStartupAdopted(this ILogger logger, string bayId, string name, int shores, string directory);

    [ZLoggerMessage(3329, LogLevel.Warning, "bay startup seeded default bayId={bayId} name={name} directory={directory}")]
    public static partial void BayStartupSeeded(this ILogger logger, string bayId, string name, string directory);

    [ZLoggerMessage(3330, LogLevel.Warning, "session restoration completed restored={restored} fresh={fresh} skipped={skipped}")]
    public static partial void SessionRestorationCompleted(this ILogger logger, int restored, int fresh, int skipped);

    [ZLoggerMessage(3331, LogLevel.Warning, "run-command relaunch on restore failed error={error}")]
    public static partial void RunCommandRelaunchFailed(this ILogger logger, string error);

    [ZLoggerMessage(3332, LogLevel.Information, "bundled adapter seeding completed copied={copied} refreshed={refreshed} userManaged={userManaged}")]
    public static partial void BundledAdaptersSeeded(this ILogger logger, int copied, int refreshed, int userManaged);

    [ZLoggerMessage(3333, LogLevel.Warning, "clean shutdown marker failed error={error}")]
    public static partial void CleanShutdownMarkerFailed(this ILogger logger, string error);

    [ZLoggerMessage(3334, LogLevel.Warning, "shutdown persistence flush failed error={error}")]
    public static partial void ShutdownPersistenceFlushFailed(this ILogger logger, string error);

    [ZLoggerMessage(3335, LogLevel.Warning, "runtime component disposal failed component={component} error={error}")]
    public static partial void RuntimeComponentDisposeFailed(this ILogger logger, string component, string error);

    [ZLoggerMessage(3336, LogLevel.Warning, "control protocol error response write failed error={error}")]
    public static partial void ControlErrorWriteFailed(this ILogger logger, string error);

    [ZLoggerMessage(3337, LogLevel.Warning, "control session disposal failed error={error}")]
    public static partial void ControlSessionDisposeFailed(this ILogger logger, string error);

    [ZLoggerMessage(3338, LogLevel.Warning, "control session faulted error={error}")]
    public static partial void ControlSessionFaulted(this ILogger logger, string error);

    [ZLoggerMessage(3339, LogLevel.Warning, "daemon idle monitor failed error={error}")]
    public static partial void IdleMonitorFailed(this ILogger logger, string error);
}
