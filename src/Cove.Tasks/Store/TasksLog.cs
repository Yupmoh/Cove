using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Tasks.Store;

internal static partial class TasksLog
{
    [ZLoggerMessage(LogLevel.Warning, "tasks.db write-channel: work item failed: {error}")]
    public static partial void WriteChannelWorkFailed(this ILogger logger, string error);

    [ZLoggerMessage(LogLevel.Warning, "tasks.db self-heal: added missing column {table}.{column} ({type})")]
    public static partial void SelfHealAddedColumn(this ILogger logger, string table, string column, string type);

    [ZLoggerMessage(LogLevel.Warning, "tasks.db self-heal: non-additive change detected on {table}.{column}: declared={declared} expected={expected} (not auto-healed; requires explicit migration)")]
    public static partial void SelfHealNonAdditiveChange(this ILogger logger, string table, string column, string declared, string expected);
}
