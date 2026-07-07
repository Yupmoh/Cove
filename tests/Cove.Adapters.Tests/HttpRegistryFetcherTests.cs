using System.Net;
using System.Net.Http;
using System.Text;
using Cove.Adapters;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class HttpRegistryFetcherTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public HttpRequestMessage? LastRequest;
        public int CallCount;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task FetchAsync_ReturnsRawJson_On200()
    {
        var json = """{"schemaVersion":2,"adapters":[{"name":"x"}]}""";
        var handler = new StubHandler(HttpStatusCode.OK, json);
        var fetcher = new HttpRegistryFetcher("https://example.com/registry.json", handler);

        var result = await fetcher.FetchAsync();

        Assert.Equal(json, result);
    }

    [Fact]
    public async Task FetchAsync_SendsRawAcceptHeader()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{}");
        var fetcher = new HttpRegistryFetcher("https://example.com/registry.json", handler);

        await fetcher.FetchAsync();

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("application/vnd.github.raw+json", handler.LastRequest!.Headers.Accept.ToString());
    }

    [Fact]
    public async Task FetchAsync_ReturnsNull_On404()
    {
        var handler = new StubHandler(HttpStatusCode.NotFound, "");
        var fetcher = new HttpRegistryFetcher("https://example.com/registry.json", handler);

        var result = await fetcher.FetchAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchAsync_ReturnsNull_OnNon200()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError, "");
        var fetcher = new HttpRegistryFetcher("https://example.com/registry.json", handler);

        var result = await fetcher.FetchAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchAsync_SwallowsHttpRequestException_ReturnsNull()
    {
        var handler = new ThrowingHandler();
        var fetcher = new HttpRegistryFetcher("https://example.com/registry.json", handler);

        var result = await fetcher.FetchAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchAsync_SwallowsTimeout_ReturnsNull()
    {
        var handler = new TimeoutHandler();
        var fetcher = new HttpRegistryFetcher("https://example.com/registry.json", handler, timeout: TimeSpan.FromMilliseconds(50));

        var result = await fetcher.FetchAsync();

        Assert.Null(result);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("offline");
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }
    }
}

public sealed class FileRegistryFetcherTests
{
    [Fact]
    public async Task FetchAsync_ReturnsFileContents_WhenExists()
    {
        var path = Path.Combine(Path.GetTempPath(), "cove-filefetcher-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var json = """{"schemaVersion":2,"adapters":[]}""";
            await File.WriteAllTextAsync(path, json);
            var fetcher = new FileRegistryFetcher(path);

            var result = await fetcher.FetchAsync();

            Assert.Equal(json, result);
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [Fact]
    public async Task FetchAsync_ReturnsNull_WhenMissing()
    {
        var fetcher = new FileRegistryFetcher(Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid().ToString("N") + ".json"));

        var result = await fetcher.FetchAsync();

        Assert.Null(result);
    }
}
