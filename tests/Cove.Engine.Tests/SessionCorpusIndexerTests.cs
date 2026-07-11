using Cove.Engine.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SessionCorpusIndexerTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-vault-" + System.Guid.NewGuid().ToString("N"));

    private static (string dir, SessionCorpusIndexer indexer) NewIndexer()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var kernel = new KnowledgePersistenceKernel(dir, NullLogger.Instance);
        kernel.EnsureAllSchemas();
        return (dir, new SessionCorpusIndexer(dir, NullLogger.Instance));
    }

    [Fact]
    public void IndexSession_ThenSearch_FindsViaFts()
    {
        var (_, indexer) = NewIndexer();
        var id = indexer.IndexSession("ws1", "claude", "2026-07-08T10:00:00Z", "The flibbertigibbet module was refactored today", "v1.0");

        var results = indexer.SearchSessions("ws1", "flibbertigibbet");
        Assert.Single(results);
        Assert.Equal(id, results[0].Id);
        Assert.Equal("claude", results[0].Adapter);
    }

    [Fact]
    public void SearchSessionsTrigram_FindsSubstring()
    {
        var (_, indexer) = NewIndexer();
        indexer.IndexSession("ws1", "claude", "2026-07-08T10:00:00Z", "implementing the routing module", "v1.0");

        var results = indexer.SearchSessionsTrigram("ws1", "rout");
        Assert.NotEmpty(results);
    }

    [Fact]
    public void ListSessions_ReturnsAllForBay()
    {
        var (_, indexer) = NewIndexer();
        indexer.IndexSession("ws1", "claude", "2026-07-08T10:00:00Z", "session one", "v1.0");
        indexer.IndexSession("ws1", "claude", "2026-07-08T11:00:00Z", "session two", "v1.0");
        indexer.IndexSession("ws2", "claude", "2026-07-08T12:00:00Z", "other bay", "v1.0");

        var ws1 = indexer.ListSessions("ws1");
        var ws2 = indexer.ListSessions("ws2");
        Assert.Equal(2, ws1.Count);
        Assert.Single(ws2);
    }

    [Fact]
    public void ReindexIfVersionChanged_UpdatesStaleSessions()
    {
        var (_, indexer) = NewIndexer();
        indexer.IndexSession("ws1", "claude", "2026-07-08T10:00:00Z", "old session", "v1.0");
        indexer.IndexSession("ws1", "claude", "2026-07-08T11:00:00Z", "new session", "v2.0");

        var reindexed = indexer.ReindexIfVersionChanged("ws1", "v2.0");
        Assert.Equal(1, reindexed);

        var sessions = indexer.ListSessions("ws1");
        Assert.All(sessions, s => Assert.Equal("v2.0", s.ExtractorVersion));
    }

    [Fact]
    public void ReindexIfVersionChanged_NoStale_ReturnsZero()
    {
        var (_, indexer) = NewIndexer();
        indexer.IndexSession("ws1", "claude", "2026-07-08T10:00:00Z", "session", "v2.0");

        var reindexed = indexer.ReindexIfVersionChanged("ws1", "v2.0");
        Assert.Equal(0, reindexed);
    }

    [Fact]
    public void SetSessionEnded_UpdatesEndedAt()
    {
        var (_, indexer) = NewIndexer();
        var id = indexer.IndexSession("ws1", "claude", "2026-07-08T10:00:00Z", "session", "v1.0");

        indexer.SetSessionEnded(id, "2026-07-08T11:00:00Z");

        var sessions = indexer.ListSessions("ws1");
        Assert.Equal("2026-07-08T11:00:00Z", sessions[0].EndedAt);
    }

    [Fact]
    public void SearchSessions_FiltersByBay()
    {
        var (_, indexer) = NewIndexer();
        indexer.IndexSession("ws1", "claude", "2026-07-08T10:00:00Z", "shared content about routing", "v1.0");
        indexer.IndexSession("ws2", "claude", "2026-07-08T11:00:00Z", "shared content about routing", "v1.0");

        var ws1 = indexer.SearchSessions("ws1", "routing");
        var ws2 = indexer.SearchSessions("ws2", "routing");
        Assert.Single(ws1);
        Assert.Single(ws2);
    }
}
