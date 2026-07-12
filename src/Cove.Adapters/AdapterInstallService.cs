using System.IO;
using System.Text.Json;
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
    private readonly MethodRunner _runner;
    private readonly ILogger<AdapterInstallService>? _logger;

    public AdapterInstallService(MethodRunner runner, ILogger<AdapterInstallService>? logger = null)
    {
        _runner = runner;
        _logger = logger;
    }

    public async Task<InstalledAdapter> InstallAsync(string adaptersRoot, string name, IAdapterFetcher fetcher, CancellationToken ct = default)
    {
        var finalDir = Path.Combine(adaptersRoot, name);
        var tempDir = Path.Combine(adaptersRoot, ".installing-" + name);

        CleanupDir(tempDir, _logger);
        Directory.CreateDirectory(tempDir);

        try
        {
            await fetcher.FetchIntoAsync(tempDir, ct).ConfigureAwait(false);

            SetExecutableBits(tempDir, _logger);

            var manifestPath = Path.Combine(tempDir, "adapter.json");
            if (!File.Exists(manifestPath))
                throw new AdapterInstallException($"adapter.json missing for {name}");

            AdapterManifest manifest;
            try
            {
                var json = await File.ReadAllTextAsync(manifestPath, ct).ConfigureAwait(false);
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

            if (Directory.Exists(finalDir))
                Directory.Delete(finalDir, recursive: true);
            Directory.Move(tempDir, finalDir);

            InstallSkill(finalDir, manifest, name);

            await RunHookAsync(finalDir, "install", name, ct, timeoutSeconds: 30).ConfigureAwait(false);

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
            CleanupDir(tempDir, _logger);
            throw;
        }
    }

    public async Task UninstallAsync(string adaptersRoot, string name, AdapterManifest? manifest = null, string? skillInstallPath = null, CancellationToken ct = default)
    {
        var adapterDir = Path.Combine(adaptersRoot, name);
        if (!Directory.Exists(adapterDir))
            return;

        await RunHookAsync(adapterDir, "uninstall", name, ct, timeoutSeconds: 5).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(skillInstallPath) && File.Exists(skillInstallPath))
        {
            try { File.Delete(skillInstallPath); }
            catch (IOException ex) { _logger?.SkillRemoveFailed(name, skillInstallPath, ex.Message); }
        }

        try
        {
            Directory.Delete(adapterDir, recursive: true);
        }
        catch (IOException ex)
        {
            _logger?.UninstallDirDeleteFailed(name, adapterDir, ex.Message);
        }
    }

    private void VerifyReferencedScripts(string dir, AdapterManifest manifest, string name)
    {
        foreach (var (method, def) in manifest.Methods)
        {
            if (def.Script is { } script && !File.Exists(Path.Combine(dir, script)))
                _logger?.ReferencedScriptMissing(name, script);
        }
        if (manifest.SessionExtractor is { } se && !File.Exists(Path.Combine(dir, se.Script)))
            _logger?.ReferencedScriptMissing(name, se.Script);
        if (manifest.Retention is { } ret)
        {
            if (ret.ReadScript is { } rs && !File.Exists(Path.Combine(dir, rs)))
                _logger?.ReferencedScriptMissing(name, rs);
            if (ret.WriteScript is { } ws && !File.Exists(Path.Combine(dir, ws)))
                _logger?.ReferencedScriptMissing(name, ws);
        }
    }

    private void InstallSkill(string adapterDir, AdapterManifest manifest, string name)
    {
        if (string.IsNullOrEmpty(manifest.SkillInstallPath))
            return;
        var source = Path.Combine(adapterDir, "skill.md");
        if (!File.Exists(source))
        {
            _logger?.SkillInstallSkipped(name, manifest.SkillInstallPath);
            return;
        }
        try
        {
            var destDir = Path.GetDirectoryName(manifest.SkillInstallPath);
            if (destDir is not null) Directory.CreateDirectory(destDir);
            File.Copy(source, manifest.SkillInstallPath, overwrite: true);
        }
        catch (IOException ex)
        {
            _logger?.SkillInstallFailed(name, manifest.SkillInstallPath, ex.Message);
        }
    }

    private async Task RunHookAsync(string adapterDir, string @event, string adapterName, CancellationToken ct, int timeoutSeconds)
    {
        var hookPath = Path.Combine(adapterDir, "hooks.sh");
        if (!File.Exists(hookPath))
            return;

        var bashExe = BashLocator.Find();
        if (bashExe is null)
        {
            _logger?.HookSkippedNoBash(adapterName, @event);
            return;
        }

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = bashExe,
            WorkingDirectory = adapterDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(hookPath);
        psi.ArgumentList.Add(@event);
        psi.Environment["COVE_ADAPTER_DIR"] = adapterDir;
        psi.Environment["COVE_EVENT"] = @event;
        psi.Environment["COVE_SDK_VERSION"] = "2";

        using var process = new System.Diagnostics.Process { StartInfo = psi };
        process.Start();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            if (process.ExitCode != 0 && @event == "install")
                _logger?.InstallHookFailed(adapterName, adapterDir, process.ExitCode);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (Exception ex) { _logger?.AdapterHookKillFailed(adapterName, @event, ex.Message); }
            if (@event == "uninstall")
                _logger?.UninstallHookTimeout(adapterName, adapterDir);
        }
    }

    private static void SetExecutableBits(string dir, ILogger? logger)
    {
        if (OperatingSystem.IsWindows()) return;
        foreach (var file in Directory.EnumerateFiles(dir, "*.sh", SearchOption.AllDirectories))
        {
            try
            {
                System.IO.File.SetUnixFileMode(file, System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite | System.IO.UnixFileMode.UserExecute | System.IO.UnixFileMode.GroupRead | System.IO.UnixFileMode.OtherRead);
            }
            catch (IOException ex) { logger?.AdapterSetExecutableFailed(file, ex.Message); }
        }
    }

    private static void CleanupDir(string dir, ILogger? logger)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch (IOException ex) { logger?.AdapterCleanupDirFailed(dir, ex.Message); }
    }
}
