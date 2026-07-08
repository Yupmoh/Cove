using System.Text;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Pty;

public sealed record ScrollbackMatch(long Offset, int Length, string ContextBefore, string ContextAfter);

public sealed class ScrollbackSearchService
{
    private readonly ILogger _logger;

    public ScrollbackSearchService(ILogger? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    public IReadOnlyList<ScrollbackMatch> Search(PtyRingBuffer ring, string query, bool caseSensitive = false, int maxResults = 1000, int contextChars = 40)
    {
        if (string.IsNullOrEmpty(query))
        {
            _logger.LogWarning("scrollback-search: query required");
            return [];
        }

        var head = ring.Head;
        var tail = ring.Tail;
        var available = (int)(head - tail);
        if (available == 0)
            return [];

        var buffer = new byte[available];
        ring.ReadInto(tail, buffer);
        var text = Encoding.UTF8.GetString(buffer);

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var queryBytes = Encoding.UTF8.GetBytes(query);

        var results = new System.Collections.Generic.List<ScrollbackMatch>();
        var searchFrom = 0;

        while (searchFrom < text.Length && results.Count < maxResults)
        {
            var idx = text.IndexOf(query, searchFrom, comparison);
            if (idx < 0)
                break;

            var contextStart = Math.Max(0, idx - contextChars);
            var contextEnd = Math.Min(text.Length, idx + query.Length + contextChars);
            var before = text.Substring(contextStart, idx - contextStart);
            var after = text.Substring(idx + query.Length, contextEnd - idx - query.Length);

            var byteOffset = tail + Encoding.UTF8.GetByteCount(text.Substring(0, idx));
            results.Add(new ScrollbackMatch(byteOffset, queryBytes.Length, before, after));
            searchFrom = idx + 1;
        }

        _logger.LogInformation("scrollback-search: found {count} matches for query in {bytes} bytes", results.Count, available);
        return results;
    }

    public ScrollbackMatch? SearchFirst(PtyRingBuffer ring, string query, bool caseSensitive = false, int contextChars = 40)
    {
        var results = Search(ring, query, caseSensitive, maxResults: 1, contextChars);
        return results.Count > 0 ? results[0] : null;
    }

    public int CountMatches(PtyRingBuffer ring, string query, bool caseSensitive = false)
    {
        if (string.IsNullOrEmpty(query))
        {
            _logger.LogWarning("scrollback-search: query required for count");
            return 0;
        }

        var head = ring.Head;
        var tail = ring.Tail;
        var available = (int)(head - tail);
        if (available == 0)
            return 0;

        var buffer = new byte[available];
        ring.ReadInto(tail, buffer);
        var text = Encoding.UTF8.GetString(buffer);

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var count = 0;
        var searchFrom = 0;
        while (searchFrom < text.Length)
        {
            var idx = text.IndexOf(query, searchFrom, comparison);
            if (idx < 0)
                break;
            count++;
            searchFrom = idx + 1;
        }
        return count;
    }
}
