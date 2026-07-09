using System.Net;
using System.Net.Http.Headers;
using Cove.Gui;
using Xunit;

public class MediaServeTests
{
    private static async Task<(LoopbackServer server, string filePath, byte[] bytes)> StartWithMediaAsync()
    {
        var tmp = Directory.CreateTempSubdirectory().FullName;
        await File.WriteAllTextAsync(Path.Combine(tmp, "index.html"), "<html></html>");
        var media = Path.Combine(tmp, "clip.bin");
        var bytes = new byte[1000];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i % 256);
        await File.WriteAllBytesAsync(media, bytes);
        var server = new LoopbackServer(tmp, _ => throw new NotImplementedException(), "0.1.0", "dev", port: 0);
        server.Start();
        return (server, media, bytes);
    }

    [Fact]
    public async Task WholeFile_200_WithAcceptRanges()
    {
        var (server, media, bytes) = await StartWithMediaAsync();
        await using var _ = server;
        using var http = new HttpClient();
        var resp = await http.GetAsync($"http://127.0.0.1:{server.Port}/media?path={Uri.EscapeDataString(media)}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("bytes", resp.Headers.AcceptRanges.ToString());
        var body = await resp.Content.ReadAsByteArrayAsync();
        Assert.Equal(bytes, body);
    }

    [Fact]
    public async Task RangeRequest_206_WithContentRange()
    {
        var (server, media, bytes) = await StartWithMediaAsync();
        await using var _ = server;
        using var http = new HttpClient();
        var req = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{server.Port}/media?path={Uri.EscapeDataString(media)}");
        req.Headers.Range = new RangeHeaderValue(100, 199);
        var resp = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.PartialContent, resp.StatusCode);
        Assert.Equal(100, resp.Content.Headers.ContentRange!.From);
        Assert.Equal(199, resp.Content.Headers.ContentRange.To);
        Assert.Equal(1000, resp.Content.Headers.ContentRange.Length);
        var body = await resp.Content.ReadAsByteArrayAsync();
        Assert.Equal(100, body.Length);
        Assert.Equal(bytes.AsSpan(100, 100).ToArray(), body);
    }

    [Fact]
    public async Task UnsatisfiableRange_416()
    {
        var (server, media, _) = await StartWithMediaAsync();
        await using var _s = server;
        using var http = new HttpClient();
        var req = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{server.Port}/media?path={Uri.EscapeDataString(media)}");
        req.Headers.Range = new RangeHeaderValue(5000, 6000);
        var resp = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, resp.StatusCode);
    }

    [Fact]
    public async Task MissingFile_404()
    {
        var (server, media, _) = await StartWithMediaAsync();
        await using var _s = server;
        using var http = new HttpClient();
        var resp = await http.GetAsync($"http://127.0.0.1:{server.Port}/media?path={Uri.EscapeDataString(media + ".nope")}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
