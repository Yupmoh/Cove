using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Cove.Adapters;

public interface IHarnessRegistryClient
{
    Task<string?> GetLatestVersionAsync(string package, CancellationToken cancellationToken = default);
}

public sealed class NpmHarnessRegistryClient : IHarnessRegistryClient
{
    private static readonly Uri DefaultEndpoint = new("https://registry.npmjs.org/");
    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;

    public NpmHarnessRegistryClient(HttpClient httpClient, Uri? endpoint = null)
    {
        _httpClient = httpClient;
        _endpoint = endpoint ?? DefaultEndpoint;
    }

    public async Task<string?> GetLatestVersionAsync(string package, CancellationToken cancellationToken = default)
    {
        var escapedPackage = Uri.EscapeDataString(package);
        var requestUri = new Uri(_endpoint, $"{escapedPackage}/latest");
        using var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var latest = JsonSerializer.Deserialize(body, AdaptersJsonContext.Default.NpmLatestVersion);
        return latest?.Version;
    }
}

public sealed record NpmLatestVersion(string? Version);

public sealed class HarnessUpdateChecker
{
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromMinutes(10);
    private readonly IHarnessRegistryClient _registryClient;
    private readonly TimeProvider _time;
    private readonly TimeSpan _cacheTtl;
    private readonly ILogger? _logger;
    private readonly object _lock = new();
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);

    private sealed record CacheEntry(string? Version, DateTimeOffset FetchedAt);

    public HarnessUpdateChecker(
        IHarnessRegistryClient registryClient,
        TimeProvider? time = null,
        TimeSpan? cacheTtl = null,
        ILogger? logger = null)
    {
        _registryClient = registryClient;
        _time = time ?? TimeProvider.System;
        _cacheTtl = cacheTtl ?? DefaultCacheTtl;
        _logger = logger;
    }

    public static HarnessUpdateChecker CreateNpm(ILogger? logger = null)
    {
        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        return new HarnessUpdateChecker(new NpmHarnessRegistryClient(httpClient), logger: logger);
    }

    public async Task<string?> GetLatestVersionAsync(string package, CancellationToken ct = default)
    {
        var now = _time.GetUtcNow();
        lock (_lock)
        {
            if (_cache.TryGetValue(package, out var hit) && now - hit.FetchedAt < _cacheTtl)
                return hit.Version;
        }

        string? version = null;
        try
        {
            version = await _registryClient.GetLatestVersionAsync(package, ct).ConfigureAwait(false);
            if (version is null)
                _logger?.HarnessLatestUnavailable(package);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            _logger?.HarnessLatestFetchFailed(package, ex.Message);
        }

        lock (_lock)
        {
            _cache[package] = new CacheEntry(version, now);
        }
        return version;
    }

    public static bool IsNewer(string latest, string installed)
    {
        if (string.IsNullOrWhiteSpace(latest) || string.IsNullOrWhiteSpace(installed))
            return false;
        var a = ParseParts(latest);
        var b = ParseParts(installed);
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var x = i < a.Length ? a[i] : 0;
            var y = i < b.Length ? b[i] : 0;
            if (x != y)
                return x > y;
        }
        return false;
    }

    private static int[] ParseParts(string version)
    {
        var parts = version.Trim().TrimStart('v', 'V').Split('.');
        var nums = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var digits = 0;
            while (digits < parts[i].Length && char.IsAsciiDigit(parts[i][digits]))
                digits++;
            nums[i] = digits == 0 ? 0 : int.Parse(parts[i].AsSpan(0, digits), CultureInfo.InvariantCulture);
        }
        return nums;
    }
}
