using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Migrations;

internal static partial class MigrationLog
{
    [ZLoggerMessage(LogLevel.Information, "running migration to version {version}: {name}")]
    public static partial void MigrationRunning(this ILogger logger, int version, string name);
}
