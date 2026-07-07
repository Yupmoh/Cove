using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Config;

internal static partial class ConfigLog
{
    [ZLoggerMessage(LogLevel.Warning, "failed to parse config at {path}: {error}")]
    public static partial void ConfigParseFailed(this ILogger logger, string path, string error);

    [ZLoggerMessage(LogLevel.Warning, "failed to read config at {path} after retries")]
    public static partial void ConfigReadFailed(this ILogger logger, string path);
}
