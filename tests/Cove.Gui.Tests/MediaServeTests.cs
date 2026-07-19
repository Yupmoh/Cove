using System.Net;
using System.Net.Http.Headers;
using Cove.Gui;
using Cove.Gui.Tests;
using Xunit;

public class MediaServeTests
{
    private sealed class MediaFixture : IAsyncDisposable
    {
        public MediaFixture(GuiTestDirectory directory, LoopbackServer server, MediaLeaseRegistry leases, string filePath, byte[] bytes)
        {
            Directory = directory;
            Server = server;
            Leases = leases;
            FilePath = filePath;
            Bytes = bytes;
        }

        public GuiTestDirectory Directory { get; }
        public LoopbackServer Server { get; }
        public MediaLeaseRegistry Leases { get; }
        public string FilePath { get; }
        public byte[] Bytes { get; }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await Server.DisposeAsync();
            }
            finally
            {
                Directory.Dispose();
            }
        }
    }

    private static async Task<MediaFixture> StartWithMediaAsync(MediaLeaseRegistry? leases = null)
    {
        var directory = GuiTestDirectory.Create("cove-media-serve-");
        LoopbackServer? server = null;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory.Path, "index.html"), "<html></html>");
            var media = Path.Combine(directory.Path, "clip.mp4");
            var bytes = new byte[1000];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i % 256);
            await File.WriteAllBytesAsync(media, bytes);
            var registry = leases ?? new MediaLeaseRegistry();
            server = new LoopbackServer(directory.Path, _ => throw new NotImplementedException(), "0.1.0", "dev", Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, port: 0, mediaLeases: registry);
            server.Start();
            return new MediaFixture(directory, server, registry, media, bytes);
        }
        catch
        {
            try
            {
                if (server is not null)
                    await server.DisposeAsync();
            }
            finally
            {
                directory.Dispose();
            }
            throw;
        }
    }

    [Fact]
    public async Task ValidLease_WholeFile_200_WithAcceptRanges()
    {
        await using var fx = await StartWithMediaAsync();
        var lease = fx.Leases.Issue(fx.FilePath);
        using var http = new HttpClient();
        var resp = await http.GetAsync($"http://127.0.0.1:{fx.Server.Port}/media?lease={lease}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("bytes", resp.Headers.AcceptRanges.ToString());
        Assert.Equal("video/mp4", resp.Content.Headers.ContentType!.MediaType);
        var body = await resp.Content.ReadAsByteArrayAsync();
        Assert.Equal(fx.Bytes, body);
    }

    [Fact]
    public async Task ValidLease_RangeRequest_206_WithContentRange()
    {
        await using var fx = await StartWithMediaAsync();
        var lease = fx.Leases.Issue(fx.FilePath);
        using var http = new HttpClient();
        var req = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{fx.Server.Port}/media?lease={lease}");
        req.Headers.Range = new RangeHeaderValue(100, 199);
        var resp = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.PartialContent, resp.StatusCode);
        Assert.Equal(100, resp.Content.Headers.ContentRange!.From);
        Assert.Equal(199, resp.Content.Headers.ContentRange.To);
        Assert.Equal(1000, resp.Content.Headers.ContentRange.Length);
        var body = await resp.Content.ReadAsByteArrayAsync();
        Assert.Equal(100, body.Length);
        Assert.Equal(fx.Bytes.AsSpan(100, 100).ToArray(), body);
    }

    [Fact]
    public async Task ValidLease_UnsatisfiableRange_416()
    {
        await using var fx = await StartWithMediaAsync();
        var lease = fx.Leases.Issue(fx.FilePath);
        using var http = new HttpClient();
        var req = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{fx.Server.Port}/media?lease={lease}");
        req.Headers.Range = new RangeHeaderValue(5000, 6000);
        var resp = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, resp.StatusCode);
    }

    [Fact]
    public async Task UnknownLease_404()
    {
        await using var fx = await StartWithMediaAsync();
        using var http = new HttpClient();
        var resp = await http.GetAsync($"http://127.0.0.1:{fx.Server.Port}/media?lease={new string('A', 64)}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task ExpiredLease_404()
    {
        var now = DateTimeOffset.UtcNow;
        var registry = new MediaLeaseRegistry(TimeSpan.FromMinutes(5), () => now);
        await using var fx = await StartWithMediaAsync(registry);
        var lease = fx.Leases.Issue(fx.FilePath);
        now += TimeSpan.FromMinutes(6);
        using var http = new HttpClient();
        var resp = await http.GetAsync($"http://127.0.0.1:{fx.Server.Port}/media?lease={lease}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task RawPathParam_IsNotAccepted_404()
    {
        await using var fx = await StartWithMediaAsync();
        using var http = new HttpClient();
        var resp = await http.GetAsync($"http://127.0.0.1:{fx.Server.Port}/media?path={Uri.EscapeDataString(fx.FilePath)}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task LeasedFileDeleted_404()
    {
        await using var fx = await StartWithMediaAsync();
        var lease = fx.Leases.Issue(fx.FilePath);
        File.Delete(fx.FilePath);
        using var http = new HttpClient();
        var resp = await http.GetAsync($"http://127.0.0.1:{fx.Server.Port}/media?lease={lease}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task ServerWithoutRegistry_404()
    {
        using var directory = GuiTestDirectory.Create("cove-media-serve-");
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "index.html"), "<html></html>");
        await using var server = new LoopbackServer(directory.Path, _ => throw new NotImplementedException(), "0.1.0", "dev", port: 0);
        server.Start();
        using var http = new HttpClient();
        var resp = await http.GetAsync($"http://127.0.0.1:{server.Port}/media?lease={new string('B', 64)}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
