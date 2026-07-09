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
    [ZLoggerMessage(LogLevel.Warning, "hook skipped no bash found adapter={adapter} event={hookEvent}")]
    public static partial void HookSkippedNoBash(this ILogger logger, string adapter, string hookEvent);

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

    [ZLoggerMessage(LogLevel.Warning, "launch profile load rejected invalid slug={slug}")]
    public static partial void LaunchProfileLoadInvalidSlug(this ILogger logger, string slug);

    [ZLoggerMessage(LogLevel.Warning, "launch profile delete failed slug={slug} error={error}")]
    public static partial void LaunchProfileDeleteFailed(this ILogger logger, string slug, string error);

    [ZLoggerMessage(LogLevel.Warning, "env store load failed adapter={adapter} error={error}")]
    public static partial void EnvStoreLoadFailed(this ILogger logger, string adapter, string error);

    [ZLoggerMessage(LogLevel.Warning, "env store delete failed adapter={adapter} error={error}")]
    public static partial void EnvStoreDeleteFailed(this ILogger logger, string adapter, string error);

    [ZLoggerMessage(LogLevel.Warning, "env store save rejected invalid adapter={adapter}")]
    public static partial void EnvStoreSaveRejectedInvalidAdapter(this ILogger logger, string adapter);

    [ZLoggerMessage(LogLevel.Warning, "adapter reload watcher error error={error}")]
    public static partial void AdapterReloadWatcherError(this ILogger logger, string error);

    [ZLoggerMessage(LogLevel.Warning, "adapter reload handler failed adapter={adapter} error={error}")]
    public static partial void AdapterReloadHandlerFailed(this ILogger logger, string adapter, string error);

    [ZLoggerMessage(LogLevel.Warning, "manifest parsed to null adapter={adapter}")]
    public static partial void ManifestParsedNull(this ILogger logger, string adapter);

    [ZLoggerMessage(LogLevel.Warning, "manifest load rejected invalid adapter={adapter}")]
    public static partial void ManifestLoadInvalidAdapter(this ILogger logger, string adapter);

    [ZLoggerMessage(LogLevel.Warning, "manifest load failed adapter={adapter} error={error}")]
    public static partial void ManifestLoadFailed(this ILogger logger, string adapter, string error);

    [ZLoggerMessage(LogLevel.Warning, "registry fetch failed url={url} error={error}")]
    public static partial void RegistryFetchFailed(this ILogger logger, string url, string error);
}
