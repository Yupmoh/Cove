using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Sessions;

internal static partial class SessionLog
{
    [ZLoggerMessage(LogLevel.Warning, "session {op} requested for unknown nook {nookId}")]
    public static partial void SessionUnknownNook(this ILogger logger, string op, string nookId);
}
