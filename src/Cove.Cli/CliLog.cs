using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Cli;

public static partial class CliLog
{
    [ZLoggerMessage(LogLevel.Debug, "cli invoked args={args}")]
    public static partial void Invoked(this ILogger logger, string args);
}
