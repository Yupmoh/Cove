using Cove.Engine.Knowledge;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class MemoryStoreTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-mem-" + System.Guid.NewGuid().ToString("N"));

    private static (string dir, MemoryStore store) NewStore()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var kernel = new KnowledgePersistenceKernel(dir, NullLogger.Instance);
        kernel.EnsureAllSchemas();
        return (dir, new MemoryStore(dir, NullLogger.Instance));
    }

    [Fact]
    public void AddFact_ThenGet_ReturnsFact()
    {
        var (_, store) = NewStore();
        var fact = store.AddFact(new Fact { BayId = "ws1", Kind = "decision", Content = "Use SQLite for all knowledge stores", Confidence = 0.9 });

        var retrieved = store.GetFact("ws1", fact.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("decision", retrieved!.Kind);
        Assert.Equal("Use SQLite for all knowledge stores", retrieved.Content);
        Assert.Equal(0.9, retrieved.Confidence);
    }

    [Fact]
    public void Supersede_ClosesPredecessorAndChains()
    {
        var (_, store) = NewStore();
        var oldFact = store.AddFact(new Fact { BayId = "ws1", Kind = "decision", Content = "Use Postgres", Confidence = 0.5 });
        var newFact = store.Supersede("ws1", oldFact.Id, new Fact { BayId = "ws1", Kind = "decision", Content = "Use SQLite instead", Confidence = 0.9 });

        Assert.NotNull(newFact);
        var oldAfter = store.GetFact("ws1", oldFact.Id);
        Assert.NotNull(oldAfter);
        Assert.Equal(newFact!.Id, oldAfter!.SupersededBy);

        var chain = store.GetSupersedeChain("ws1", oldFact.Id);
        Assert.Equal(2, chain.Count);
        Assert.Equal(oldFact.Id, chain[0].Id);
        Assert.Equal(newFact.Id, chain[1].Id);
    }

    [Fact]
    public void SupersedeChain_ExtendsAcrossMultipleSupersessions()
    {
        var (_, store) = NewStore();
        var v1 = store.AddFact(new Fact { BayId = "ws1", Kind = "preference", Content = "v1", Confidence = 0.5 });
        var v2 = store.Supersede("ws1", v1.Id, new Fact { BayId = "ws1", Kind = "preference", Content = "v2", Confidence = 0.7 });
        var v3 = store.Supersede("ws1", v2!.Id, new Fact { BayId = "ws1", Kind = "preference", Content = "v3", Confidence = 0.9 });

        var chain = store.GetSupersedeChain("ws1", v1.Id);
        Assert.Equal(3, chain.Count);
        Assert.Equal("v1", chain[0].Content);
        Assert.Equal("v2", chain[1].Content);
        Assert.Equal("v3", chain[2].Content);
    }

    [Fact]
    public void ListFacts_ExcludesSuperseded()
    {
        var (_, store) = NewStore();
        var old = store.AddFact(new Fact { BayId = "ws1", Kind = "decision", Content = "old", Confidence = 0.3 });
        store.Supersede("ws1", old.Id, new Fact { BayId = "ws1", Kind = "decision", Content = "new", Confidence = 0.9 });
        store.AddFact(new Fact { BayId = "ws1", Kind = "gotcha", Content = "standalone", Confidence = 0.5 });

        var facts = store.ListFacts("ws1");
        Assert.Equal(2, facts.Count);
        Assert.DoesNotContain(facts, f => f.Id == old.Id);
    }

    [Fact]
    public void SearchFacts_FindsViaFts()
    {
        var (_, store) = NewStore();
        store.AddFact(new Fact { BayId = "ws1", Kind = "decision", Content = "The flibbertigibbet module handles routing", Confidence = 0.8 });
        store.AddFact(new Fact { BayId = "ws1", Kind = "gotcha", Content = "unrelated content", Confidence = 0.5 });

        var results = store.SearchFacts("ws1", "flibbertigibbet");
        Assert.Single(results);
        Assert.Contains("flibbertigibbet", results[0].Content);
    }

    [Fact]
    public void ReindexFromDisk_RebuildsFactsFromFiles()
    {
        var (dir, store) = NewStore();
        store.AddFact(new Fact { BayId = "ws1", Kind = "decision", Content = "rebuildable fact", Confidence = 0.7 });
        store.AddFact(new Fact { BayId = "ws1", Kind = "preference", Content = "another fact", Confidence = 0.5 });

        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={System.IO.Path.Combine(dir, "memory", "memory.db")}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM facts;";
            cmd.ExecuteNonQuery();
        }

        Assert.Empty(store.ListFacts("ws1"));

        store.ReindexFromDisk("ws1");

        var facts = store.ListFacts("ws1");
        Assert.Equal(2, facts.Count);
    }

    [Fact]
    public void FactOffloadsToFile()
    {
        var (dir, store) = NewStore();
        var fact = store.AddFact(new Fact { BayId = "ws1", Kind = "decision", Content = "offloaded", Confidence = 0.6 });

        var factFile = System.IO.Path.Combine(dir, "memory", "facts", "ws1", fact.Id + ".json");
        Assert.True(System.IO.File.Exists(factFile));
        var content = System.IO.File.ReadAllText(factFile);
        Assert.Contains("offloaded", content);
    }

    [Fact]
    public void IncrementAccessCount_IncrementsCount()
    {
        var (_, store) = NewStore();
        var fact = store.AddFact(new Fact { BayId = "ws1", Kind = "decision", Content = "test", Confidence = 0.5 });

        store.IncrementAccessCount("ws1", fact.Id);
        store.IncrementAccessCount("ws1", fact.Id);

        var retrieved = store.GetFact("ws1", fact.Id);
        Assert.Equal(2, retrieved!.AccessCount);
    }
}
