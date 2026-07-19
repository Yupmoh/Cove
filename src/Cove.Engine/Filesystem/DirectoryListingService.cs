using Cove.Platform;
using Cove.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cove.Engine.Filesystem;

public sealed class DirectoryListingService
{
    private const int MaximumEntries = 400;
    private readonly IPlatformFileSystem _fileSystem;
    private readonly ILogger _logger;

    public DirectoryListingService(IPlatformFileSystem fileSystem, ILogger? logger = null)
    {
        _fileSystem = fileSystem;
        _logger = logger ?? NullLogger.Instance;
    }

    public DirectoryListResult List(string path, int cap = MaximumEntries)
    {
        if (!_fileSystem.DirectoryExists(path))
        {
            _logger.DirectoryListingRejected(path, "not_found");
            return new DirectoryListResult([], false, "not_found");
        }

        var effectiveCap = Math.Clamp(cap, 1, MaximumEntries);
        var entries = new List<DirectoryEntryDto>(effectiveCap);
        try
        {
            foreach (var entry in _fileSystem.EnumerateFileSystemEntries(path))
            {
                if (entries.Count == effectiveCap)
                {
                    entries.Sort(CompareEntries);
                    return new DirectoryListResult(entries, true, null);
                }

                entries.Add(new DirectoryEntryDto(
                    Path.GetFileName(entry),
                    _fileSystem.DirectoryExists(entry)));
            }
        }
        catch (Exception exception)
        {
            _logger.DirectoryListingRejected(path, exception.Message);
            return new DirectoryListResult([], false, "unavailable");
        }

        entries.Sort(CompareEntries);
        return new DirectoryListResult(entries, false, null);
    }

    private static int CompareEntries(DirectoryEntryDto left, DirectoryEntryDto right)
    {
        if (left.IsDir != right.IsDir)
            return left.IsDir ? -1 : 1;
        return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }
}
