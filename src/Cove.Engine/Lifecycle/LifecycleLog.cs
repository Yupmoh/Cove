using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Lifecycle;

internal static partial class LifecycleLog
{
    [ZLoggerMessage(LogLevel.Warning, "lifecycle {op} requested for unknown pane {paneId}")]
    public static partial void LifecycleUnknownPane(this ILogger logger, string op, string paneId);
}
