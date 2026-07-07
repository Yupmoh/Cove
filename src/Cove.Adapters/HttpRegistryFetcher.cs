using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace Cove.Adapters;

public sealed class HttpRegistryFetcher : IRegistryFetcher
{
    private const string RawAccept = "application/vnd.github.raw+json";
    private readonly string _url;
    private readonly HttpMessageHandler _handler;
    private readonly ILogger? _logger;
    private readonly TimeSpan _timeout;

    public HttpRegistryFetcher(string url, HttpMessageHandler handler, ILogger? logger = null, TimeSpan? timeout = null)
    {
        _url = url;
        _handler = handler;
        _logger = logger;
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    public HttpRegistryFetcher(string url, ILogger? logger = null, TimeSpan? timeout = null)
        : this(url, new HttpClientHandler(), logger, timeout)
    {
    }

    public async Task<string?> FetchAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient(_handler, disposeHandler: false) { Timeout = _timeout };
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(RawAccept));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("cove-adapter-host");

        try
        {
            using var response = await client.GetAsync(_url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                    _logger?.RegistryFetchFailed(_url, $"HTTP {(int)response.StatusCode}");
                return null;
            }
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger?.RegistryFetchFailed(_url, ex.Message);
            return null;
        }
        catch (TaskCanceledException)
        {
            _logger?.RegistryFetchFailed(_url, "timeout");
            return null;
        }
    }
}
