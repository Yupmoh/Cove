using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine;

public static partial class EnvPropagationLog
{
    [ZLoggerMessage(LogLevel.Warning, "env propagation lookup failed adapter={adapter} binary={binary} error={error}")]
    public static partial void EnvPropagationLookupFailed(this ILogger logger, string adapter, string binary, string error);

    [ZLoggerMessage(LogLevel.Information, "env propagation skipped on windows adapter={adapter} binary={binary}")]
    public static partial void EnvPropagationSkippedWindows(this ILogger logger, string adapter, string binary);

    [ZLoggerMessage(LogLevel.Warning, "env propagation signal failed adapter={adapter} binary={binary} pane={pane} error={error}")]
    public static partial void EnvPropagationSignalFailed(this ILogger logger, string adapter, string binary, string pane, string error);

    [ZLoggerMessage(LogLevel.Warning, "env propagation binary unresolved adapter={adapter}")]
    public static partial void EnvPropagationBinaryUnresolved(this ILogger logger, string adapter);

    [ZLoggerMessage(LogLevel.Warning, "env propagation manifest parse failed adapter={adapter} error={error}")]
    public static partial void EnvPropagationManifestParseFailed(this ILogger logger, string adapter, string error);
}
