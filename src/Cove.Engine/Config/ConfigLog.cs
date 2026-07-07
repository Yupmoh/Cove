using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Config;

internal static partial class ConfigLog
{
    [ZLoggerMessage(LogLevel.Warning, "failed to parse config at {path}: {error}")]
    public static partial void ConfigParseFailed(this ILogger logger, string path, string error);
}
