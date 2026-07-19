using Cove.Persistence;
using Cove.Platform;
using Cove.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cove.Engine.Feedback;

public sealed class FeedbackStore
{
    private const int MaximumSlugLength = 80;
    private readonly string _root;
    private readonly IPlatformFileSystem _fileSystem;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    public FeedbackStore(
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

    public FeedbackSaveResult Save(string json, string slug)
    {
        _fileSystem.CreateDirectory(_root);
        var safeSlug = SanitizeSlug(slug);
        var stamp = _timeProvider.GetUtcNow().ToString("yyyyMMdd-HHmmss");
        var path = Path.Combine(_root, $"{stamp}-{safeSlug}.json");
        try
        {
            AtomicJsonStore.WriteRawText(path, json, _logger);
            return new FeedbackSaveResult(path);
        }
        catch (Exception exception)
        {
            _logger.FeedbackPersistenceFailed(safeSlug, exception.Message);
            throw;
        }
    }

    private static string SanitizeSlug(string slug)
    {
        var fileName = Path.GetFileName(slug);
        if (string.IsNullOrWhiteSpace(fileName))
            return "ui-feedback";

        Span<char> buffer = stackalloc char[Math.Min(fileName.Length, MaximumSlugLength)];
        var length = 0;
        foreach (var value in fileName)
        {
            if (length == buffer.Length)
                break;
            if (char.IsAsciiLetterOrDigit(value) || value is '-' or '_')
                buffer[length++] = value;
        }
        return length == 0 ? "ui-feedback" : new string(buffer[..length]);
    }
}
