using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Lifecycle;

internal static partial class LifecycleLog
{
    [ZLoggerMessage(LogLevel.Warning, "lifecycle {op} requested for unknown nook {nookId}")]
    public static partial void LifecycleUnknownNook(this ILogger logger, string op, string nookId);
}
