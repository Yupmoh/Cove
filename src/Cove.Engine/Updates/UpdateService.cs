using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Updates;

public sealed record UpdateRelease(string Version, string Channel, string ReleaseNotesUrl, string DownloadUrl, DateTime PublishedAt, int MinimumAppVersion);
public sealed record UpdateChannel(string Name, string FeedUrl);
public sealed record UpdateCheckResult(bool UpdateAvailable, UpdateRelease? LatestRelease, string CurrentVersion, string Error);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(UpdateRelease))]
[JsonSerializable(typeof(List<UpdateRelease>))]
public sealed partial class UpdateJsonContext : JsonSerializerContext { }

public sealed class UpdateService
{
    private readonly HttpClient _http;
    private readonly ILogger _logger;
    private readonly string _currentVersion;
    private UpdateChannel _channel;

    public UpdateService(HttpClient http, string currentVersion, UpdateChannel channel, ILogger? logger = null)
    {
        _http = http;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _currentVersion = currentVersion;
        _channel = channel;
    }

    public UpdateChannel Channel => _channel;

    public void SetChannel(UpdateChannel channel)
    {
        _channel = channel;
        _logger.LogInformation("updates: channel switched to {name} ({url})", channel.Name, channel.FeedUrl);
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_channel.FeedUrl))
        {
            _logger.LogWarning("updates: no feed URL configured for channel {name}", _channel.Name);
            return new UpdateCheckResult(false, null, _currentVersion, "no feed URL configured");
        }

        try
        {
            var json = await _http.GetStringAsync(_channel.FeedUrl, ct).ConfigureAwait(false);
            var releases = JsonSerializer.Deserialize(json, UpdateJsonContext.Default.ListUpdateRelease);
            if (releases is null || releases.Count == 0)
            {
                _logger.LogWarning("updates: feed returned no releases for channel {name}", _channel.Name);
                return new UpdateCheckResult(false, null, _currentVersion, "feed returned no releases");
            }

            var latest = releases[0];
            if (latest.MinimumAppVersion > 0 && !IsVersionGreaterOrEqual(_currentVersion, latest.MinimumAppVersion.ToString()))
            {
                _logger.LogWarning("updates: latest release requires app version >= {min}, current is {cur}", latest.MinimumAppVersion, _currentVersion);
                return new UpdateCheckResult(false, null, _currentVersion, "minimum app version not met");
            }

            var updateAvailable = IsVersionGreater(latest.Version, _currentVersion);
            if (updateAvailable)
                _logger.LogInformation("updates: update available {ver} on channel {name}", latest.Version, _channel.Name);
            else
                _logger.LogDebug("updates: no update available, current {ver} on channel {name}", _currentVersion, _channel.Name);

            return new UpdateCheckResult(updateAvailable, latest, _currentVersion, "");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "updates: failed to check for updates on channel {name}", _channel.Name);
            return new UpdateCheckResult(false, null, _currentVersion, ex.Message);
        }
    }

    public async Task<string> DownloadUpdateAsync(UpdateRelease release, string destinationPath, IProgress<long>? progress = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(release.DownloadUrl))
        {
            _logger.LogWarning("updates: release {ver} has no download URL", release.Version);
            throw new InvalidOperationException("release has no download URL");
        }

        using var response = await _http.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        await using var fileStream = File.Create(destinationPath);
        await using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        var buffer = new byte[81920];
        long totalRead = 0;
        int read;

        while ((read = await contentStream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            totalRead += read;
            progress?.Report(totalRead);
        }

        _logger.LogInformation("updates: downloaded {ver} ({bytes} bytes) to {path}", release.Version, totalRead, destinationPath);
        return destinationPath;
    }

    public UpdateCheckResult CheckForUpdatesFromJson(string json)
    {
        try
        {
            var releases = JsonSerializer.Deserialize(json, UpdateJsonContext.Default.ListUpdateRelease);
            if (releases is null || releases.Count == 0)
                return new UpdateCheckResult(false, null, _currentVersion, "feed returned no releases");

            var latest = releases[0];
            var updateAvailable = IsVersionGreater(latest.Version, _currentVersion);
            return new UpdateCheckResult(updateAvailable, latest, _currentVersion, "");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "updates: failed to parse release feed JSON");
            return new UpdateCheckResult(false, null, _currentVersion, ex.Message);
        }
    }

    private static bool IsVersionGreater(string a, string b)
    {
        if (TryParseVersion(a, out var va) && TryParseVersion(b, out var vb))
            return va > vb;
        return string.CompareOrdinal(a, b) > 0;
    }

    private static bool IsVersionGreaterOrEqual(string a, string b)
    {
        if (TryParseVersion(a, out var va) && TryParseVersion(b, out var vb))
            return va >= vb;
        return string.CompareOrdinal(a, b) >= 0;
    }

    private static bool TryParseVersion(string s, out Version v)
    {
        var trimmed = s.TrimStart('v', 'V');
        return Version.TryParse(trimmed, out v!);
    }
}
