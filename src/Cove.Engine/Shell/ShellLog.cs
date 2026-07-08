using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Shell;

internal static partial class ShellLog
{
    [ZLoggerMessage(LogLevel.Warning, "managed-shell: cli binary not found at {path}")]
    public static partial void ManagedShellCliMissing(this ILogger logger, string path);

    [ZLoggerMessage(LogLevel.Warning, "managed-shell: reinstall failed for {path}: {error}")]
    public static partial void ManagedShellReinstallFailed(this ILogger logger, string path, string error);

    [ZLoggerMessage(LogLevel.Warning, "managed-shell: chmod failed for {path}: {error}")]
    public static partial void ManagedShellChmodFailed(this ILogger logger, string path, string error);
}
