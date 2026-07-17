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

    [ZLoggerMessage(LogLevel.Warning, "harness latest version fetch failed package={package} error={error}")]
    public static partial void HarnessLatestFetchFailed(this ILogger logger, string package, string error);

    [ZLoggerMessage(LogLevel.Warning, "harness latest version unavailable package={package}")]
    public static partial void HarnessLatestUnavailable(this ILogger logger, string package);

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

    [ZLoggerMessage(LogLevel.Warning, "manifest load rejected invalid adapter={adapter}")]
    public static partial void ManifestLoadInvalidAdapter(this ILogger logger, string adapter);

    [ZLoggerMessage(LogLevel.Warning, "registry fetch failed url={url} error={error}")]
    public static partial void RegistryFetchFailed(this ILogger logger, string url, string error);

    [ZLoggerMessage(LogLevel.Information, "bundled adapter seeded adapter={adapter}")]
    public static partial void BundledAdapterSeeded(this ILogger logger, string adapter);

    [ZLoggerMessage(LogLevel.Information, "bundled adapter refreshed adapter={adapter}")]
    public static partial void BundledAdapterRefreshed(this ILogger logger, string adapter);

    [ZLoggerMessage(LogLevel.Debug, "bundled adapter left untouched (user-managed, no stamp) adapter={adapter}")]
    public static partial void BundledAdapterUserManaged(this ILogger logger, string adapter);

    [ZLoggerMessage(LogLevel.Warning, "bundled adapter source directory not found baseDir={baseDir}")]
    public static partial void BundledAdapterSourceMissing(this ILogger logger, string baseDir);

    [ZLoggerMessage(LogLevel.Warning, "bundled adapter stamp read failed path={path} error={error}")]
    public static partial void BundledStampReadFailed(this ILogger logger, string path, string error);

    [ZLoggerMessage(LogLevel.Warning, "bundled adapter set executable bit failed file={file} error={error}")]
    public static partial void BundledSetExecutableFailed(this ILogger logger, string file, string error);

    [ZLoggerMessage(LogLevel.Information, "binary probe started commands={commands} source={source} pathDirCount={pathDirCount} wellKnownCount={wellKnownCount}")]
    public static partial void BinaryProbeStarted(this ILogger logger, string commands, string source, int pathDirCount, int wellKnownCount);

    [ZLoggerMessage(LogLevel.Debug, "binary probe path contents source={source} rawPath={rawPath}")]
    public static partial void BinaryProbePathContents(this ILogger logger, string source, string rawPath);

    [ZLoggerMessage(LogLevel.Debug, "binary candidate tested command={command} candidate={candidate} exists={exists} probe={probe}")]
    public static partial void BinaryCandidateTested(this ILogger logger, string command, string candidate, bool exists, string probe);

    [ZLoggerMessage(LogLevel.Information, "binary resolved command={command} path={path} probe={probe}")]
    public static partial void BinaryResolved(this ILogger logger, string command, string path, string probe);

    [ZLoggerMessage(LogLevel.Warning, "binary resolved to non-runnable posix-style path on windows command={command} path={path} hint=CreateProcessW-requires-a-native-windows-path")]
    public static partial void BinaryResolvedNonRunnableWindowsPath(this ILogger logger, string command, string path);

    [ZLoggerMessage(LogLevel.Warning, "binary probe found no candidate commands={commands} pathDirCount={pathDirCount} wellKnownCount={wellKnownCount}")]
    public static partial void BinaryProbeMissing(this ILogger logger, string commands, int pathDirCount, int wellKnownCount);

    [ZLoggerMessage(LogLevel.Information, "binary version probed path={path} version={version} state={state}")]
    public static partial void BinaryVersionProbed(this ILogger logger, string path, string version, string state);

    [ZLoggerMessage(LogLevel.Warning, "binary version probe failed path={path} error={error}")]
    public static partial void BinaryVersionProbeFailed(this ILogger logger, string path, string error);

    [ZLoggerMessage(LogLevel.Warning, "binary version probe kill failed path={path} error={error}")]
    public static partial void BinaryVersionProbeKillFailed(this ILogger logger, string path, string error);

    [ZLoggerMessage(LogLevel.Warning, "method run skipped no bash available adapter={adapter} script={script}")]
    public static partial void MethodNoBash(this ILogger logger, string adapter, string script);

    [ZLoggerMessage(LogLevel.Information, "method completed adapter={adapter} script={script} exitCode={exitCode} durationMs={durationMs}")]
    public static partial void MethodCompleted(this ILogger logger, string adapter, string script, int exitCode, long durationMs);

    [ZLoggerMessage(LogLevel.Warning, "method stderr adapter={adapter} script={script} stderr={stderr}")]
    public static partial void MethodStderr(this ILogger logger, string adapter, string script, string stderr);

    [ZLoggerMessage(LogLevel.Debug, "method stdout digest adapter={adapter} script={script} bytes={bytes} head={head}")]
    public static partial void MethodStdoutDigest(this ILogger logger, string adapter, string script, int bytes, string head);

    [ZLoggerMessage(LogLevel.Warning, "method timed out adapter={adapter} script={script} timeoutMs={timeoutMs} killed=true")]
    public static partial void MethodTimedOut(this ILogger logger, string adapter, string script, long timeoutMs);

    [ZLoggerMessage(LogLevel.Debug, "method stdout not json adapter={adapter} script={script} error={error}")]
    public static partial void MethodStdoutNotJson(this ILogger logger, string adapter, string script, string error);

    [ZLoggerMessage(LogLevel.Warning, "method kill failed adapter={adapter} script={script} error={error}")]
    public static partial void MethodKillFailed(this ILogger logger, string adapter, string script, string error);

    [ZLoggerMessage(LogLevel.Debug, "session list cache hit adapter={adapter} cwd={cwd} count={count}")]
    public static partial void SessionListCacheHit(this ILogger logger, string adapter, string cwd, int count);

    [ZLoggerMessage(LogLevel.Debug, "session list completed adapter={adapter} cwd={cwd} count={count}")]
    public static partial void SessionListCompleted(this ILogger logger, string adapter, string cwd, int count);

    [ZLoggerMessage(LogLevel.Warning, "session list failed adapter={adapter} cwd={cwd} exitCode={exitCode}")]
    public static partial void SessionListFailed(this ILogger logger, string adapter, string cwd, int exitCode);

    [ZLoggerMessage(LogLevel.Warning, "session list parse failed adapter={adapter} error={error}")]
    public static partial void SessionListParseFailed(this ILogger logger, string adapter, string error);

    [ZLoggerMessage(LogLevel.Warning, "session extract failed adapter={adapter} script={script} exitCode={exitCode}")]
    public static partial void SessionExtractFailed(this ILogger logger, string adapter, string script, int exitCode);

    [ZLoggerMessage(LogLevel.Debug, "session event parse failed adapter={adapter} error={error}")]
    public static partial void SessionEventParseFailed(this ILogger logger, string adapter, string error);

    [ZLoggerMessage(LogLevel.Debug, "manifest not found adapter={adapter} path={path}")]
    public static partial void ManifestNotFound(this ILogger logger, string adapter, string path);

    [ZLoggerMessage(LogLevel.Debug, "manifest cache hit adapter={adapter}")]
    public static partial void ManifestCacheHit(this ILogger logger, string adapter);

    [ZLoggerMessage(LogLevel.Debug, "manifest loaded adapter={adapter} path={path}")]
    public static partial void ManifestLoaded(this ILogger logger, string adapter, string path);

    [ZLoggerMessage(LogLevel.Debug, "manifest dir skipped empty name dir={dir}")]
    public static partial void ManifestDirSkippedEmptyName(this ILogger logger, string dir);

    [ZLoggerMessage(LogLevel.Warning, "manifest load failed adapter={adapter} path={path} error={error}")]
    public static partial void ManifestLoadFailedAt(this ILogger logger, string adapter, string path, string error);

    [ZLoggerMessage(LogLevel.Warning, "manifest parsed to null adapter={adapter} path={path}")]
    public static partial void ManifestParsedNullAt(this ILogger logger, string adapter, string path);

    [ZLoggerMessage(LogLevel.Warning, "manifest screenState dropped (invalid rules) adapter={adapter}")]
    public static partial void ManifestScreenStateDropped(this ILogger logger, string adapter);

    [ZLoggerMessage(LogLevel.Debug, "manifest validation rule failed adapter={adapter} field={field} code={code}")]
    public static partial void ManifestValidationRuleFailed(this ILogger logger, string adapter, string field, string code);

    [ZLoggerMessage(LogLevel.Information, "manifest validation summary adapter={adapter} errorCount={errorCount}")]
    public static partial void ManifestValidationSummary(this ILogger logger, string adapter, int errorCount);

    [ZLoggerMessage(LogLevel.Warning, "manifest parse failed error={error}")]
    public static partial void ManifestParseFailed(this ILogger logger, string error);

    [ZLoggerMessage(LogLevel.Warning, "registry disk cache read failed path={path} error={error}")]
    public static partial void RegistryDiskReadFailed(this ILogger logger, string path, string error);

    [ZLoggerMessage(LogLevel.Warning, "registry cache write failed path={path} error={error}")]
    public static partial void RegistryCacheWriteFailed(this ILogger logger, string path, string error);

    [ZLoggerMessage(LogLevel.Warning, "registry stale cache read failed path={path} error={error}")]
    public static partial void RegistryStaleReadFailed(this ILogger logger, string path, string error);

    [ZLoggerMessage(LogLevel.Warning, "registry fetch threw error={error}")]
    public static partial void RegistryFetchThrew(this ILogger logger, string error);

    [ZLoggerMessage(LogLevel.Warning, "registry json parse failed error={error}")]
    public static partial void RegistryParseFailed(this ILogger logger, string error);

    [ZLoggerMessage(LogLevel.Debug, "registry served from cache source={source}")]
    public static partial void RegistryServedFromCache(this ILogger logger, string source);

    [ZLoggerMessage(LogLevel.Warning, "skill scan directory access denied dir={dir} error={error}")]
    public static partial void SkillScanAccessDenied(this ILogger logger, string dir, string error);

    [ZLoggerMessage(LogLevel.Warning, "skill scan directory missing dir={dir}")]
    public static partial void SkillScanDirMissing(this ILogger logger, string dir);

    [ZLoggerMessage(LogLevel.Warning, "skill parse failed path={path} error={error}")]
    public static partial void SkillParseFailed(this ILogger logger, string path, string error);

    [ZLoggerMessage(LogLevel.Debug, "skill parse skipped missing frontmatter path={path}")]
    public static partial void SkillParseNoFrontmatter(this ILogger logger, string path);

    [ZLoggerMessage(LogLevel.Warning, "skill watcher error error={error}")]
    public static partial void SkillWatcherError(this ILogger logger, string error);

    [ZLoggerMessage(LogLevel.Warning, "adapter set executable bit failed file={file} error={error}")]
    public static partial void AdapterSetExecutableFailed(this ILogger logger, string file, string error);

    [ZLoggerMessage(LogLevel.Warning, "adapter cleanup dir failed dir={dir} error={error}")]
    public static partial void AdapterCleanupDirFailed(this ILogger logger, string dir, string error);

    [ZLoggerMessage(LogLevel.Warning, "adapter hook kill failed adapter={adapter} event={hookEvent} error={error}")]
    public static partial void AdapterHookKillFailed(this ILogger logger, string adapter, string hookEvent, string error);

    [ZLoggerMessage(LogLevel.Warning, "launch profile parse failed path={path} error={error}")]
    public static partial void LaunchProfileParseFailed(this ILogger logger, string path, string error);

    [ZLoggerMessage(LogLevel.Warning, "nook selection parse failed adapter={adapter} path={path} error={error}")]
    public static partial void NookSelectionParseFailed(this ILogger logger, string adapter, string path, string error);
}
