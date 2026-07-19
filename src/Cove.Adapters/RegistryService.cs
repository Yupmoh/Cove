using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Platform;
using Microsoft.Extensions.Logging;

namespace Cove.Adapters;

public static class RegistryConstants
{
    public const string RegistryContentsUrl = "https://api.github.com/repos/jonnyasmar/atrium-adapters/contents/registry.json";
    public const string DevSiblingDir = "../atrium-adapters";
}
public sealed record Registry
{
    private readonly int _schemaVersion = 1;
    public int SchemaVersion { get => _schemaVersion == 0 ? 1 : _schemaVersion; init => _schemaVersion = value; }
    public required IReadOnlyList<RegistryEntry> Adapters { get; init; }
}

public sealed record ParityResult(
    bool ParityOk,
    bool UpdateAvailable,
    bool Compatible,
    string? DriftWarning);

public interface IRegistryFetcher
{
    Task<string?> FetchAsync(CancellationToken cancellationToken = default);
}

public sealed class RegistryService
{
    private Registry? _cached;
    private DateTimeOffset _cachedAt;
    private readonly TimeSpan _cacheTtl;
    private readonly string? _cachePath;
    private readonly IRegistryFetcher? _fetcher;
    private readonly ILogger? _logger;
    private readonly TimeProvider _time;
    private readonly IPlatformFileSystem _fileSystem;

    public RegistryService(
        TimeSpan? cacheTtl = null,
        ILogger? logger = null,
        TimeProvider? time = null,
        IPlatformFileSystem? fileSystem = null)
    {
        _cacheTtl = cacheTtl ?? TimeSpan.FromHours(1);
        _logger = logger;
        _time = time ?? TimeProvider.System;
        _fileSystem = fileSystem ?? SystemPlatformFileSystem.Instance;
    }

    public RegistryService(
        string cachePath,
        IRegistryFetcher fetcher,
        TimeSpan? cacheTtl = null,
        ILogger? logger = null,
        TimeProvider? time = null,
        IPlatformFileSystem? fileSystem = null)
    {
        _cachePath = cachePath;
        _fetcher = fetcher;
        _cacheTtl = cacheTtl ?? TimeSpan.FromHours(1);
        _logger = logger;
        _time = time ?? TimeProvider.System;
        _fileSystem = fileSystem ?? SystemPlatformFileSystem.Instance;
    }

    public static Registry? ParseRegistry(string json, ILogger? logger = null)
    {
        try
        {
            return JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.Registry);
        }
        catch (JsonException ex)
        {
            logger?.RegistryParseFailed(ex.Message);
            return null;
        }
    }

    public Registry? GetCached() => _cachedAt != default && _time.GetUtcNow() - _cachedAt < _cacheTtl ? _cached : null;

    public void SetCache(Registry registry)
    {
        _cached = registry;
        _cachedAt = _time.GetUtcNow();
    }

    public async Task<Registry?> FetchAsync(CancellationToken cancellationToken = default)
    {
        if (GetCached() is { } memCached)
        {
            _logger?.RegistryServedFromCache("memory");
            return memCached;
        }

        Registry? diskCached = null;
        if (_cachePath is not null && _fileSystem.FileExists(_cachePath))
        {
            try
            {
                var diskAge = _time.GetUtcNow() - _fileSystem.GetLastWriteTimeUtc(_cachePath);
                if (diskAge < _cacheTtl)
                {
                    var diskJson = await _fileSystem.ReadAllTextAsync(_cachePath, cancellationToken).ConfigureAwait(false);
                    diskCached = ParseRegistry(diskJson, _logger);
                    if (diskCached is not null)
                    {
                        SetCache(diskCached);
                        _logger?.RegistryServedFromCache("disk");
                        return diskCached;
                    }
                }
            }
            catch (IOException ex) { _logger?.RegistryDiskReadFailed(_cachePath, ex.Message); }
        }

        if (_fetcher is not null)
        {
            string? fetchedJson = null;
            bool fetchSucceeded = false;
            try
            {
                fetchedJson = await _fetcher.FetchAsync(cancellationToken).ConfigureAwait(false);
                fetchSucceeded = fetchedJson is not null;
            }
            catch (Exception ex)
            {
                _logger?.RegistryFetchThrew(ex.Message);
                fetchSucceeded = false;
            }

            if (fetchSucceeded && fetchedJson is not null)
            {
                var reg = ParseRegistry(fetchedJson, _logger);
                if (reg is not null)
                {
                    SetCache(reg);
                    if (_cachePath is not null)
                    {
                        try
                        {
                            var dir = Path.GetDirectoryName(_cachePath);
                            if (dir is not null) _fileSystem.CreateDirectory(dir);
                            await _fileSystem.WriteAllTextAsync(_cachePath, fetchedJson, cancellationToken).ConfigureAwait(false);
                        }
                        catch (IOException ex) { _logger?.RegistryCacheWriteFailed(_cachePath, ex.Message); }
                    }
                    return reg;
                }
            }

            if (diskCached is not null)
                return diskCached;
            if (_cachePath is not null && _fileSystem.FileExists(_cachePath))
            {
                try
                {
                    var staleJson = await _fileSystem.ReadAllTextAsync(_cachePath, cancellationToken).ConfigureAwait(false);
                    var staleReg = ParseRegistry(staleJson, _logger);
                    if (staleReg is not null)
                    {
                        SetCache(staleReg);
                        _logger?.RegistryServedFromCache("stale-disk");
                        return staleReg;
                    }
                }
                catch (IOException ex) { _logger?.RegistryStaleReadFailed(_cachePath, ex.Message); }
            }
            if (_cached is not null)
                return _cached;
        }
        return diskCached ?? _cached;
    }

    public static ParityResult CheckParity(RegistryEntry registryEntry, string installedVersion, string? appVersion = null)
    {
        var parityOk = registryEntry.Version == installedVersion;
        var updateAvailable = CompareVersions(registryEntry.Version, installedVersion) > 0;

        var compatible = string.IsNullOrEmpty(registryEntry.MinAppVersion)
            || CompareVersions(appVersion ?? "0.0.0", registryEntry.MinAppVersion) >= 0;

        string? driftWarning = null;
        if (!parityOk)
            driftWarning = $"installed {installedVersion} vs registry {registryEntry.Version}";

        return new ParityResult(parityOk, updateAvailable, compatible, driftWarning);
    }

    private static int CompareVersions(string a, string b)
    {
        var partsA = a.Split('.');
        var partsB = b.Split('.');
        var maxLen = Math.Max(partsA.Length, partsB.Length);
        for (int i = 0; i < maxLen; i++)
        {
            var va = i < partsA.Length && int.TryParse(partsA[i], out var pa) ? pa : 0;
            var vb = i < partsB.Length && int.TryParse(partsB[i], out var pb) ? pb : 0;
            if (va != vb)
                return va - vb;
        }
        return 0;
    }
}
