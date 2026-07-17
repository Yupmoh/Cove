using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Cove.Adapters;

public sealed class HarnessUpdateChecker
{
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromMinutes(10);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    private readonly Func<string, CancellationToken, Task<string?>> _fetchLatest;
    private readonly TimeProvider _time;
    private readonly TimeSpan _cacheTtl;
    private readonly ILogger? _logger;
    private readonly object _lock = new();
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);

    private sealed record CacheEntry(string? Version, DateTimeOffset FetchedAt);

    public HarnessUpdateChecker(
        Func<string, CancellationToken, Task<string?>> fetchLatest,
        TimeProvider? time = null,
        TimeSpan? cacheTtl = null,
        ILogger? logger = null)
    {
        _fetchLatest = fetchLatest;
        _time = time ?? TimeProvider.System;
        _cacheTtl = cacheTtl ?? DefaultCacheTtl;
        _logger = logger;
    }

    public static HarnessUpdateChecker CreateNpm(ILogger? logger = null)
        => new(FetchNpmLatestAsync, logger: logger);

    private static async Task<string?> FetchNpmLatestAsync(string package, CancellationToken ct)
    {
        using var response = await Http.GetAsync($"https://registry.npmjs.org/{package}/latest", ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("version", out var version) ? version.GetString() : null;
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
            version = await _fetchLatest(package, ct).ConfigureAwait(false);
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
