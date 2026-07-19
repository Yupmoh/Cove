using System.Text.Json;
using Cove.Platform;
using Microsoft.Extensions.Logging;

namespace Cove.Adapters;

public interface IAdapterFetcher
{
    Task FetchIntoAsync(string destDir, CancellationToken cancellationToken = default);
}

public sealed class AdapterInstallException : Exception
{
    public AdapterInstallException(string message) : base(message) { }
    public AdapterInstallException(string message, Exception inner) : base(message, inner) { }
}

public sealed class AdapterInstallService
{
    private readonly IPlatformFileSystem _fileSystem;
    private readonly IExecutableMode _executableMode;
    private readonly IBashLocator _bashLocator;
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<AdapterInstallService>? _logger;

    public AdapterInstallService(
        IPlatformFileSystem? fileSystem = null,
        IExecutableMode? executableMode = null,
        IBashLocator? bashLocator = null,
        IProcessRunner? processRunner = null,
        ILogger<AdapterInstallService>? logger = null)
    {
        _fileSystem = fileSystem ?? SystemPlatformFileSystem.Instance;
        _executableMode = executableMode ?? new SystemExecutableMode();
        _bashLocator = bashLocator ?? new BashLocator(_fileSystem);
        _processRunner = processRunner ?? new SystemProcessRunner();
        _logger = logger;
    }

    public async Task<InstalledAdapter> InstallAsync(string adaptersRoot, string name, IAdapterFetcher fetcher, CancellationToken ct = default)
    {
        var finalDir = Path.Combine(adaptersRoot, name);
        var tempDir = Path.Combine(adaptersRoot, ".installing-" + name);

        CleanupDir(tempDir);
        _fileSystem.CreateDirectory(tempDir);

        try
        {
            await fetcher.FetchIntoAsync(tempDir, ct).ConfigureAwait(false);
            SetExecutableBits(tempDir);

            var manifestPath = Path.Combine(tempDir, "adapter.json");
            if (!_fileSystem.FileExists(manifestPath))
                throw new AdapterInstallException($"adapter.json missing for {name}");

            AdapterManifest manifest;
            try
            {
                var json = await _fileSystem.ReadAllTextAsync(manifestPath, ct).ConfigureAwait(false);
                manifest = JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.AdapterManifest)
                    ?? throw new AdapterInstallException($"adapter.json is null for {name}");
            }
            catch (JsonException ex)
            {
                throw new AdapterInstallException($"adapter.json is invalid for {name}", ex);
            }

            var errors = ManifestValidator.Validate(manifest, _logger);
            if (errors.Count > 0)
            {
                _logger?.ManifestValidationFailed(name, errors[0].Field, errors[0].Code);
                throw new AdapterInstallException($"manifest validation failed for {name}: {errors[0].Field} {errors[0].Code}");
            }

            VerifyReferencedScripts(tempDir, manifest, name);
            if (_fileSystem.DirectoryExists(finalDir))
                _fileSystem.DeleteDirectory(finalDir, recursive: true);
            _fileSystem.MoveDirectory(tempDir, finalDir);

            InstallSkill(finalDir, manifest, name);
            await RunHookAsync(finalDir, "install", name, ct, TimeSpan.FromSeconds(30)).ConfigureAwait(false);

            return new InstalledAdapter
            {
                Name = name,
                Dir = finalDir,
                Manifest = manifest,
                DetectionState = AdapterDetectionState.Detected,
            };
        }
        catch
        {
            CleanupDir(tempDir);
            throw;
        }
    }

    public async Task UninstallAsync(string adaptersRoot, string name, AdapterManifest? manifest = null, string? skillInstallPath = null, CancellationToken ct = default)
    {
        var adapterDir = Path.Combine(adaptersRoot, name);
        if (!_fileSystem.DirectoryExists(adapterDir))
        {
            _logger?.AdapterUninstallDirectoryMissing(name, adapterDir);
            return;
        }

        await RunHookAsync(adapterDir, "uninstall", name, ct, TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(skillInstallPath) && _fileSystem.FileExists(skillInstallPath))
        {
            try { _fileSystem.DeleteFile(skillInstallPath); }
            catch (IOException ex) { _logger?.SkillRemoveFailed(name, skillInstallPath, ex.Message); }
        }

        try
        {
            _fileSystem.DeleteDirectory(adapterDir, recursive: true);
        }
        catch (IOException ex)
        {
            _logger?.UninstallDirDeleteFailed(name, adapterDir, ex.Message);
        }
    }

    private void VerifyReferencedScripts(string directory, AdapterManifest manifest, string name)
    {
        foreach (var definition in manifest.Methods.Values)
            if (definition.Script is { } script && !_fileSystem.FileExists(Path.Combine(directory, script)))
                _logger?.ReferencedScriptMissing(name, script);
        if (manifest.SessionExtractor is { } extractor && !_fileSystem.FileExists(Path.Combine(directory, extractor.Script)))
            _logger?.ReferencedScriptMissing(name, extractor.Script);
        if (manifest.Retention is { } retention)
        {
            if (!_fileSystem.FileExists(Path.Combine(directory, retention.ReadScript)))
                _logger?.ReferencedScriptMissing(name, retention.ReadScript);
            if (!_fileSystem.FileExists(Path.Combine(directory, retention.WriteScript)))
                _logger?.ReferencedScriptMissing(name, retention.WriteScript);
        }
    }

    private void InstallSkill(string adapterDir, AdapterManifest manifest, string name)
    {
        if (string.IsNullOrEmpty(manifest.SkillInstallPath))
            return;
        var source = Path.Combine(adapterDir, "skill.md");
        if (!_fileSystem.FileExists(source))
        {
            _logger?.SkillInstallSkipped(name, manifest.SkillInstallPath);
            return;
        }
        try
        {
            var destinationDirectory = Path.GetDirectoryName(manifest.SkillInstallPath);
            if (destinationDirectory is not null)
                _fileSystem.CreateDirectory(destinationDirectory);
            _fileSystem.CopyFile(source, manifest.SkillInstallPath, overwrite: true);
        }
        catch (IOException ex)
        {
            _logger?.SkillInstallFailed(name, manifest.SkillInstallPath, ex.Message);
        }
    }

    private async Task RunHookAsync(string adapterDir, string eventName, string adapterName, CancellationToken ct, TimeSpan timeout)
    {
        var hookPath = Path.Combine(adapterDir, "hooks.sh");
        if (!_fileSystem.FileExists(hookPath))
            return;

        var bash = _bashLocator.Find();
        if (bash is null)
        {
            _logger?.HookSkippedNoBash(adapterName, eventName);
            return;
        }

        var environment = new Dictionary<string, string>(3, StringComparer.Ordinal)
        {
            ["COVE_ADAPTER_DIR"] = adapterDir,
            ["COVE_EVENT"] = eventName,
            ["COVE_SDK_VERSION"] = "2",
        };
        ProcessRunResult result;
        try
        {
            result = await _processRunner.RunAsync(
                new ProcessRunRequest(bash, adapterDir, [hookPath, eventName], environment, timeout),
                ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.AdapterHookProcessFailed(adapterName, eventName, ex.Message);
            return;
        }

        if (!result.Started)
        {
            _logger?.AdapterHookProcessFailed(adapterName, eventName, "process did not start");
            return;
        }
        if (result.TimedOut)
        {
            _logger?.AdapterHookProcessFailed(adapterName, eventName, "process timed out");
            if (eventName == "uninstall")
                _logger?.UninstallHookTimeout(adapterName, adapterDir);
            return;
        }
        if (result.ExitCode != 0 && eventName == "install")
            _logger?.InstallHookFailed(adapterName, adapterDir, result.ExitCode);
    }

    private void SetExecutableBits(string directory)
    {
        foreach (var file in _fileSystem.EnumerateFiles(directory, "*.sh", SearchOption.AllDirectories))
        {
            try
            {
                _executableMode.MakeUserExecutable(file);
            }
            catch (IOException ex)
            {
                _logger?.AdapterSetExecutableFailed(file, ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger?.AdapterSetExecutableFailed(file, ex.Message);
            }
        }
    }

    private void CleanupDir(string directory)
    {
        try
        {
            if (_fileSystem.DirectoryExists(directory))
                _fileSystem.DeleteDirectory(directory, recursive: true);
        }
        catch (IOException ex)
        {
            _logger?.AdapterCleanupDirFailed(directory, ex.Message);
        }
    }
}
