using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Adapters;

internal static partial class AdapterLog
{
    [ZLoggerMessage(LogLevel.Warning, "install hook failed adapter={adapter} dir={dir} exitCode={exitCode}")]
    public static partial void InstallHookFailed(this ILogger logger, string adapter, string dir, int exitCode);

    [ZLoggerMessage(LogLevel.Warning, "uninstall hook timed out adapter={adapter} dir={dir} killed=true")]
    public static partial void UninstallHookTimeout(this ILogger logger, string adapter, string dir);

    [ZLoggerMessage(LogLevel.Warning, "uninstall dir delete failed adapter={adapter} dir={dir} error={error}")]
    public static partial void UninstallDirDeleteFailed(this ILogger logger, string adapter, string dir, string error);

    [ZLoggerMessage(LogLevel.Warning, "skill install skipped missing path adapter={adapter} skillInstallPath={path}")]
    public static partial void SkillInstallSkipped(this ILogger logger, string adapter, string path);

    [ZLoggerMessage(LogLevel.Warning, "skill install failed adapter={adapter} path={path} error={error}")]
    public static partial void SkillInstallFailed(this ILogger logger, string adapter, string path, string error);

    [ZLoggerMessage(LogLevel.Warning, "skill remove failed adapter={adapter} path={path} error={error}")]
    public static partial void SkillRemoveFailed(this ILogger logger, string adapter, string path, string error);

    [ZLoggerMessage(LogLevel.Warning, "manifest validation failed adapter={adapter} field={field} code={code}")]
    public static partial void ManifestValidationFailed(this ILogger logger, string adapter, string field, string code);

    [ZLoggerMessage(LogLevel.Warning, "referenced script missing adapter={adapter} script={script}")]
    public static partial void ReferencedScriptMissing(this ILogger logger, string adapter, string script);

    [ZLoggerMessage(LogLevel.Warning, "skill index rebuild failed error={error}")]
    public static partial void SkillRebuildFailed(this ILogger logger, string error);

    [ZLoggerMessage(LogLevel.Warning, "skill watch root missing root={root}")]
    public static partial void SkillWatchRootMissing(this ILogger logger, string root);

    [ZLoggerMessage(LogLevel.Warning, "agent load rejected invalid slug={slug}")]
    public static partial void AgentLoadInvalidSlug(this ILogger logger, string slug);

    [ZLoggerMessage(LogLevel.Warning, "agent delete rejected invalid slug={slug}")]
    public static partial void AgentDeleteInvalidSlug(this ILogger logger, string slug);

    [ZLoggerMessage(LogLevel.Warning, "agent delete failed slug={slug} error={error}")]
    public static partial void AgentDeleteFailed(this ILogger logger, string slug, string error);
}
