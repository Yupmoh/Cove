using Microsoft.Extensions.Logging;

namespace Cove.Adapters;

public sealed class FileRegistryFetcher : IRegistryFetcher
{
    private readonly string _path;
    private readonly ILogger? _logger;

    public FileRegistryFetcher(string path, ILogger? logger = null)
    {
        _path = path;
        _logger = logger;
    }

    public Task<string?> FetchAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
            return Task.FromResult<string?>(null);
        try
        {
            return Task.FromResult<string?>(File.ReadAllText(_path));
        }
        catch (IOException ex)
        {
            _logger?.RegistryFetchFailed(_path, ex.Message);
            return Task.FromResult<string?>(null);
        }
    }
}
