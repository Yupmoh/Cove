using Cove.Engine.Knowledge;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class MemoryRankerTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-memrank-" + System.Guid.NewGuid().ToString("N"));

    private static (string dir, MemoryStore store, MemoryRanker ranker) NewStack()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var kernel = new KnowledgePersistenceKernel(dir, NullLogger.Instance);
        kernel.EnsureAllSchemas();
        var store = new MemoryStore(dir, NullLogger.Instance);
        var ranker = new MemoryRanker(store, dir, NullLogger.Instance);
        return (dir, store, ranker);
    }

    [Fact]
    public void SearchRanked_ReturnsResultsSortedByFusedScore()
    {
        var (_, store, ranker) = NewStack();
        store.AddFact(new Fact { BayId = "ws1", Kind = "decision", Content = "The routing module handles request dispatch", Confidence = 0.3 });
        store.AddFact(new Fact { BayId = "ws1", Kind = "decision", Content = "The routing module was refactored for performance", Confidence = 0.9 });

        var results = ranker.SearchRanked("ws1", "routing");
        Assert.Equal(2, results.Count);
        Assert.True(results[0].Score >= results[1].Score);
    }

    [Fact]
    public void SearchRanked_IncludesSnippetAroundQuery()
    {
        var (_, store, ranker) = NewStack();
        store.AddFact(new Fact { BayId = "ws1", Kind = "gotcha", Content = "The flibbertigibbet module has a race condition that manifests under load", Confidence = 0.8 });

        var results = ranker.SearchRanked("ws1", "flibbertigibbet");
        Assert.Single(results);
        Assert.NotNull(results[0].Snippet);
        Assert.Contains("flibbertigibbet", results[0].Snippet!);
    }

    [Fact]
    public void Recall_ReturnsPreviewsWithHowLongAgo()
    {
        var (_, store, ranker) = NewStack();
        var fact = store.AddFact(new Fact { BayId = "ws1", Kind = "preference", Content = "Use dark theme for all editors", Confidence = 0.7 });

        var previews = ranker.Recall("ws1", "theme");
        Assert.NotEmpty(previews);
        Assert.Equal(fact.Id, previews[0].Id);
        Assert.False(string.IsNullOrEmpty(previews[0].HowLongAgo));
        Assert.False(string.IsNullOrEmpty(previews[0].Preview));
    }

    [Fact]
    public void Recall_IncrementsAccessCount()
    {
        var (_, store, ranker) = NewStack();
        var fact = store.AddFact(new Fact { BayId = "ws1", Kind = "decision", Content = "Use SQLite for knowledge stores", Confidence = 0.8 });

        ranker.Recall("ws1", "SQLite");
        ranker.Recall("ws1", "SQLite");

        var retrieved = store.GetFact("ws1", fact.Id);
        Assert.Equal(2, retrieved!.AccessCount);
    }

    [Fact]
    public void Recall_LogsFeedbackRow()
    {
        var (dir, store, ranker) = NewStack();
        store.AddFact(new Fact { BayId = "ws1", Kind = "decision", Content = "Use SQLite for persistence", Confidence = 0.8 });

        ranker.Recall("ws1", "SQLite");

        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={System.IO.Path.Combine(dir, "memory", "memory.db")}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM feedback";
        var count = (long)cmd.ExecuteScalar()!;
        Assert.Equal(1, count);
    }

    [Fact]
    public void SearchRanked_FiltersByBay()
    {
        var (_, store, ranker) = NewStack();
        store.AddFact(new Fact { BayId = "ws1", Kind = "decision", Content = "shared decision about routing", Confidence = 0.8 });
        store.AddFact(new Fact { BayId = "ws2", Kind = "decision", Content = "shared decision about routing", Confidence = 0.8 });

        var ws1 = ranker.SearchRanked("ws1", "routing");
        var ws2 = ranker.SearchRanked("ws2", "routing");
        Assert.Single(ws1);
        Assert.Single(ws2);
    }
}
