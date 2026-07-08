using Cove.Engine.Pty;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ScrollbackSearchServiceTests
{
    private static PtyRingBuffer CreateRing(string content)
    {
        var ring = new PtyRingBuffer(4096);
        ring.Append(System.Text.Encoding.UTF8.GetBytes(content));
        return ring;
    }

    [Fact]
    public void Search_FindsPlainText()
    {
        var ring = CreateRing("hello world hello again");
        var svc = new ScrollbackSearchService(NullLogger.Instance);
        var results = svc.Search(ring, "hello");
        Assert.Equal(2, results.Count);
        Assert.Equal(0, results[0].Offset);
        Assert.Equal(12, results[1].Offset);
    }

    [Fact]
    public void Search_CaseInsensitive()
    {
        var ring = CreateRing("Hello HELLO hello");
        var svc = new ScrollbackSearchService(NullLogger.Instance);
        var results = svc.Search(ring, "hello", caseSensitive: false);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Search_CaseSensitive()
    {
        var ring = CreateRing("Hello hello HELLO");
        var svc = new ScrollbackSearchService(NullLogger.Instance);
        var results = svc.Search(ring, "hello", caseSensitive: true);
        Assert.Single(results);
    }

    [Fact]
    public void Search_NoMatch_ReturnsEmpty()
    {
        var ring = CreateRing("nothing here");
        var svc = new ScrollbackSearchService(NullLogger.Instance);
        var results = svc.Search(ring, "missing");
        Assert.Empty(results);
    }

    [Fact]
    public void Search_EmptyQuery_ReturnsEmpty()
    {
        var ring = CreateRing("hello world");
        var svc = new ScrollbackSearchService(NullLogger.Instance);
        var results = svc.Search(ring, "");
        Assert.Empty(results);
    }

    [Fact]
    public void Search_IncludesContext()
    {
        var ring = CreateRing("AAA hello BBB");
        var svc = new ScrollbackSearchService(NullLogger.Instance);
        var results = svc.Search(ring, "hello", contextChars: 4);
        var match = Assert.Single(results);
        Assert.Equal("AAA ", match.ContextBefore);
        Assert.Equal(" BBB", match.ContextAfter);
    }

    [Fact]
    public void SearchFirst_ReturnsFirstMatch()
    {
        var ring = CreateRing("foo bar foo");
        var svc = new ScrollbackSearchService(NullLogger.Instance);
        var match = svc.SearchFirst(ring, "foo");
        Assert.NotNull(match);
        Assert.Equal(0, match!.Offset);
    }

    [Fact]
    public void SearchFirst_NoMatch_ReturnsNull()
    {
        var ring = CreateRing("hello world");
        var svc = new ScrollbackSearchService(NullLogger.Instance);
        Assert.Null(svc.SearchFirst(ring, "missing"));
    }

    [Fact]
    public void CountMatches_ReturnsCorrectCount()
    {
        var ring = CreateRing("ab ab ab ab");
        var svc = new ScrollbackSearchService(NullLogger.Instance);
        Assert.Equal(4, svc.CountMatches(ring, "ab"));
    }

    [Fact]
    public void CountMatches_NoMatch_ReturnsZero()
    {
        var ring = CreateRing("hello world");
        var svc = new ScrollbackSearchService(NullLogger.Instance);
        Assert.Equal(0, svc.CountMatches(ring, "missing"));
    }

    [Fact]
    public void Search_EmptyRing_ReturnsEmpty()
    {
        var ring = new PtyRingBuffer(4096);
        var svc = new ScrollbackSearchService(NullLogger.Instance);
        var results = svc.Search(ring, "hello");
        Assert.Empty(results);
    }

    [Fact]
    public void Search_MaxResults_LimitsResults()
    {
        var ring = CreateRing("a a a a a a a a a a");
        var svc = new ScrollbackSearchService(NullLogger.Instance);
        var results = svc.Search(ring, "a", maxResults: 3);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Search_UnicodeContent()
    {
        var ring = CreateRing("héllo wörld héllo");
        var svc = new ScrollbackSearchService(NullLogger.Instance);
        var results = svc.Search(ring, "héllo");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Search_OverlappingMatches()
    {
        var ring = CreateRing("aaa");
        var svc = new ScrollbackSearchService(NullLogger.Instance);
        var results = svc.Search(ring, "aa");
        Assert.Equal(2, results.Count);
    }
}
