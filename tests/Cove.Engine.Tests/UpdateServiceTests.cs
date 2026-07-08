using System.Net;
using System.Text.Json;
using Cove.Engine.Updates;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class UpdateServiceTests
{
    private static readonly string MockFeedJson = JsonSerializer.Serialize(new[]
    {
        new { version = "2.0.0", channel = "stable", releaseNotesUrl = "https://cove.dev/releases/2.0.0", downloadUrl = "https://cove.dev/dl/2.0.0", publishedAt = "2026-07-01T00:00:00Z", minimumAppVersion = 0 },
        new { version = "1.5.0", channel = "stable", releaseNotesUrl = "https://cove.dev/releases/1.5.0", downloadUrl = "https://cove.dev/dl/1.5.0", publishedAt = "2026-06-01T00:00:00Z", minimumAppVersion = 0 },
    });

    [Fact]
    public void CheckForUpdatesFromJson_DetectsNewerRelease()
    {
        var service = new UpdateService(new HttpClient(), "1.0.0", new UpdateChannel("stable", "https://mock"));
        var result = service.CheckForUpdatesFromJson(MockFeedJson);

        Assert.True(result.UpdateAvailable);
        Assert.NotNull(result.LatestRelease);
        Assert.Equal("2.0.0", result.LatestRelease!.Version);
        Assert.Equal("1.0.0", result.CurrentVersion);
    }

    [Fact]
    public void CheckForUpdatesFromJson_NoUpdateWhenCurrentIsLatest()
    {
        var service = new UpdateService(new HttpClient(), "2.0.0", new UpdateChannel("stable", "https://mock"));
        var result = service.CheckForUpdatesFromJson(MockFeedJson);

        Assert.False(result.UpdateAvailable);
    }

    [Fact]
    public void CheckForUpdatesFromJson_HandlesVersionVPrefix()
    {
        var json = JsonSerializer.Serialize(new[]
        {
            new { version = "v3.0.0", channel = "stable", releaseNotesUrl = "", downloadUrl = "", publishedAt = "2026-07-01T00:00:00Z", minimumAppVersion = 0 },
        });
        var service = new UpdateService(new HttpClient(), "v2.0.0", new UpdateChannel("stable", "https://mock"));
        var result = service.CheckForUpdatesFromJson(json);

        Assert.True(result.UpdateAvailable);
        Assert.Equal("v3.0.0", result.LatestRelease!.Version);
    }

    [Fact]
    public void CheckForUpdatesFromJson_EmptyFeedReturnsError()
    {
        var service = new UpdateService(new HttpClient(), "1.0.0", new UpdateChannel("stable", "https://mock"));
        var result = service.CheckForUpdatesFromJson("[]");

        Assert.False(result.UpdateAvailable);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void CheckForUpdatesFromJson_InvalidJsonReturnsError()
    {
        var service = new UpdateService(new HttpClient(), "1.0.0", new UpdateChannel("stable", "https://mock"));
        var result = service.CheckForUpdatesFromJson("not json");

        Assert.False(result.UpdateAvailable);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void SetChannel_UpdatesChannel()
    {
        var service = new UpdateService(new HttpClient(), "1.0.0", new UpdateChannel("stable", "https://stable"));
        var newChannel = new UpdateChannel("beta", "https://beta");

        service.SetChannel(newChannel);

        Assert.Equal("beta", service.Channel.Name);
        Assert.Equal("https://beta", service.Channel.FeedUrl);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_FetchesFromFeedUrl()
    {
        using var handler = new MockHttpHandler(MockFeedJson);
        using var http = new HttpClient(handler);
        var service = new UpdateService(http, "1.0.0", new UpdateChannel("stable", "https://mock.cove.dev/feed"));

        var result = await service.CheckForUpdatesAsync();

        Assert.True(result.UpdateAvailable);
        Assert.Equal("2.0.0", result.LatestRelease!.Version);
        Assert.Equal("https://mock.cove.dev/feed", handler.RequestedUrl);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_NoFeedUrlReturnsError()
    {
        var service = new UpdateService(new HttpClient(), "1.0.0", new UpdateChannel("stable", ""));
        var result = await service.CheckForUpdatesAsync();

        Assert.False(result.UpdateAvailable);
        Assert.Contains("no feed URL", result.Error);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_NetworkErrorReturnsError()
    {
        using var handler = new MockHttpHandler("", HttpStatusCode.InternalServerError);
        using var http = new HttpClient(handler);
        var service = new UpdateService(http, "1.0.0", new UpdateChannel("stable", "https://mock.cove.dev/feed"));

        var result = await service.CheckForUpdatesAsync();

        Assert.False(result.UpdateAvailable);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task DownloadUpdateAsync_DownloadsToFile()
    {
        var payload = "fake-binary-content"u8.ToArray();
        using var handler = new MockHttpHandler(System.Text.Encoding.UTF8.GetString(payload), contentType: "application/octet-stream");
        using var http = new HttpClient(handler);
        var service = new UpdateService(http, "1.0.0", new UpdateChannel("stable", "https://mock"));

        var release = new UpdateRelease("2.0.0", "stable", "", "https://mock.cove.dev/dl", DateTime.UtcNow, 0);
        var tempPath = Path.Combine(Path.GetTempPath(), $"cove-update-test-{Guid.NewGuid():N}.bin");

        try
        {
            await service.DownloadUpdateAsync(release, tempPath);
            var downloaded = await File.ReadAllBytesAsync(tempPath);
            Assert.Equal(payload, downloaded);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task DownloadUpdateAsync_ThrowsWhenNoDownloadUrl()
    {
        var service = new UpdateService(new HttpClient(), "1.0.0", new UpdateChannel("stable", "https://mock"));
        var release = new UpdateRelease("2.0.0", "stable", "", "", DateTime.UtcNow, 0);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.DownloadUpdateAsync(release, "/tmp/test.bin"));
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        private readonly HttpStatusCode _statusCode;
        private readonly string _contentType;

        public string RequestedUrl { get; private set; } = "";

        public MockHttpHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK, string contentType = "application/json")
        {
            _responseBody = responseBody;
            _statusCode = statusCode;
            _contentType = contentType;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUrl = request.RequestUri?.ToString() ?? "";
            var content = new StringContent(_responseBody);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(_contentType);
            return Task.FromResult(new HttpResponseMessage(_statusCode) { Content = content });
        }
    }
}
