using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Sessions;

internal static partial class SessionLog
{
    [ZLoggerMessage(LogLevel.Warning, "session {op} requested for unknown pane {paneId}")]
    public static partial void SessionUnknownPane(this ILogger logger, string op, string paneId);
}
