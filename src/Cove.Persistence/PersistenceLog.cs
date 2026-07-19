using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Persistence;

internal static partial class PersistenceLog
{
    [ZLoggerMessage(LogLevel.Warning, "MarkDirty for unregistered state key {key}")]
    public static partial void MarkDirtyUnregistered(this ILogger logger, string key);

    [ZLoggerMessage(LogLevel.Error, "state write failed for {key}; retaining dirty mark: {error}")]
    public static partial void StateWriteFailed(this ILogger logger, string key, string error);

    [ZLoggerMessage(LogLevel.Warning, "state {key} recovered from fallback {path}")]
    public static partial void StateRecoveredFromFallback(this ILogger logger, string key, string path);

    [ZLoggerMessage(LogLevel.Warning, "state {key} candidate {path} unreadable; trying older: {error}")]
    public static partial void StateCandidateUnreadable(this ILogger logger, string key, string path, string error);

    [ZLoggerMessage(LogLevel.Warning, "journal prune failed for {path}: {error}")]
    public static partial void JournalPruneFailed(this ILogger logger, string path, string error);

    [ZLoggerMessage(LogLevel.Error, "debounced state flush failed: {error}")]
    public static partial void DebouncedFlushFailed(this ILogger logger, string error);

    [ZLoggerMessage(LogLevel.Warning, "flat-json file {path} failed to parse; falling back to .bak: {error}")]
    public static partial void FlatJsonParseFailedFallbackBak(this ILogger logger, string path, string error);

    [ZLoggerMessage(LogLevel.Warning, "flat-json file {path} failed to parse and no .bak exists: {error}")]
    public static partial void FlatJsonParseFailedNoBak(this ILogger logger, string path, string error);

    [ZLoggerMessage(LogLevel.Debug, "atomic json write path={path} bytes={bytes} backup={backup}")]
    public static partial void AtomicWrite(this ILogger logger, string path, int bytes, bool backup);

    [ZLoggerMessage(LogLevel.Debug, "atomic json read path={path} found={found}")]
    public static partial void AtomicRead(this ILogger logger, string path, bool found);

    [ZLoggerMessage(LogLevel.Information, "sqlite migration applied version={version}")]
    public static partial void SqliteMigrationApplied(this ILogger logger, int version);

    [ZLoggerMessage(LogLevel.Information, "sqlite migrations complete fromVersion={fromVersion} toVersion={toVersion} applied={applied}")]
    public static partial void SqliteMigrationsComplete(this ILogger logger, long fromVersion, long toVersion, int applied);

}
