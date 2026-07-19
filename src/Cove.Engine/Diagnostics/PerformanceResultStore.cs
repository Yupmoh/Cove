using Cove.Persistence;
using Cove.Platform;
using Cove.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cove.Engine.Diagnostics;

public sealed class PerformanceResultStore
{
    private readonly string _root;
    private readonly IPlatformFileSystem _fileSystem;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    public PerformanceResultStore(
        string root,
        IPlatformFileSystem fileSystem,
        TimeProvider? timeProvider = null,
        ILogger? logger = null)
    {
        _root = Path.GetFullPath(root);
        _fileSystem = fileSystem;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger.Instance;
    }

    public PerformanceResultSaveResult Save(string json, string markdown)
    {
        _fileSystem.CreateDirectory(_root);
        var stamp = _timeProvider.GetUtcNow().ToString("yyyyMMdd-HHmmss");
        try
        {
            AtomicJsonStore.WriteRawText(Path.Combine(_root, $"perf-{stamp}.json"), json, _logger);
            AtomicJsonStore.WriteRawText(Path.Combine(_root, "latest.json"), json, _logger);
            AtomicJsonStore.WriteRawText(Path.Combine(_root, "latest.md"), markdown, _logger);
            return new PerformanceResultSaveResult(_root);
        }
        catch (Exception exception)
        {
            _logger.PerformanceResultPersistenceFailed(_root, exception.Message);
            throw;
        }
    }
}
