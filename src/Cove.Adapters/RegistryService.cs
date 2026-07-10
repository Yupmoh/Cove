using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    public RegistryService(TimeSpan? cacheTtl = null)
    {
        _cacheTtl = cacheTtl ?? TimeSpan.FromHours(1);
    }

    public RegistryService(string cachePath, IRegistryFetcher fetcher, TimeSpan? cacheTtl = null)
    {
        _cachePath = cachePath;
        _fetcher = fetcher;
        _cacheTtl = cacheTtl ?? TimeSpan.FromHours(1);
    }

    public static Registry? ParseRegistry(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.Registry);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public Registry? GetCached() => _cachedAt != default && DateTimeOffset.UtcNow - _cachedAt < _cacheTtl ? _cached : null;

    public void SetCache(Registry registry)
    {
        _cached = registry;
        _cachedAt = DateTimeOffset.UtcNow;
    }

    public async Task<Registry?> FetchAsync(CancellationToken cancellationToken = default)
    {
        if (GetCached() is { } memCached)
            return memCached;

        Registry? diskCached = null;
        if (_cachePath is not null && File.Exists(_cachePath))
        {
            try
            {
                var diskAge = DateTimeOffset.UtcNow - File.GetLastWriteTimeUtc(_cachePath);
                if (diskAge < _cacheTtl)
                {
                    var diskJson = await File.ReadAllTextAsync(_cachePath, cancellationToken).ConfigureAwait(false);
                    diskCached = ParseRegistry(diskJson);
                    if (diskCached is not null)
                    {
                        SetCache(diskCached);
                        return diskCached;
                    }
                }
            }
            catch (IOException) { }
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
            catch (Exception)
            {
                fetchSucceeded = false;
            }

            if (fetchSucceeded && fetchedJson is not null)
            {
                var reg = ParseRegistry(fetchedJson);
                if (reg is not null)
                {
                    SetCache(reg);
                    if (_cachePath is not null)
                    {
                        try
                        {
                            var dir = Path.GetDirectoryName(_cachePath);
                            if (dir is not null) Directory.CreateDirectory(dir);
                            await File.WriteAllTextAsync(_cachePath, fetchedJson, cancellationToken).ConfigureAwait(false);
                        }
                        catch (IOException) { }
                    }
                    return reg;
                }
            }

            if (diskCached is not null)
                return diskCached;
            if (_cachePath is not null && File.Exists(_cachePath))
            {
                try
                {
                    var staleJson = await File.ReadAllTextAsync(_cachePath, cancellationToken).ConfigureAwait(false);
                    var staleReg = ParseRegistry(staleJson);
                    if (staleReg is not null)
                    {
                        SetCache(staleReg);
                        return staleReg;
                    }
                }
                catch (IOException) { }
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
