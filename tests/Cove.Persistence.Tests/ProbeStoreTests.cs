using Cove.Persistence;
using Cove.Testing;
using Xunit;

namespace Cove.Persistence.Tests;

public sealed class ProbeStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;

    public ProbeStoreTests()
    {
        SqliteBootstrap.EnsureInitialized();
        _dbPath = Path.Combine(Path.GetTempPath(), $"cove-probe-test-{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory(_dbPath);
    }

    public void Dispose()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            TestFile.Delete(_dbPath + suffix);
    }

    private static ProbeRow[] SampleRows() => new[]
    {
        new ProbeRow(Guid.CreateVersion7().ToString("D"), 1L, "Harbor status", "the deep teal harbor is calm tonight"),
        new ProbeRow(Guid.CreateVersion7().ToString("D"), 2L, "Build log", "native aot publish succeeded on three rids"),
        new ProbeRow(Guid.CreateVersion7().ToString("D"), 3L, "Notes", "flat json state written atomically"),
    };

    [Fact]
    public void CreateSchema_DoesNotThrow_Fts5ModulePresent()
    {
        var store = new ProbeStore(_factory);
        var ex = Record.Exception(() => store.CreateSchema());
        Assert.Null(ex);
    }

    [Fact]
    public void InsertThenCount_ReturnsInsertedRowCount()
    {
        var store = new ProbeStore(_factory);
        store.CreateSchema();
        foreach (var r in SampleRows()) store.Insert(r);
        Assert.Equal(3L, store.Count());
    }

    [Fact]
    public void Search_Harbor_ReturnsExactlyOneHit()
    {
        var store = new ProbeStore(_factory);
        store.CreateSchema();
        foreach (var r in SampleRows()) store.Insert(r);
        var hits = store.Search("harbor");
        Assert.Single(hits);
        Assert.Equal("Harbor status", hits[0].Title);
    }

    [Fact]
    public void ConnectionFactory_SetsWalJournalMode()
    {
        using var conn = _factory.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode;";
        var mode = (string)(cmd.ExecuteScalar() ?? "unknown");
        Assert.Equal("wal", mode, ignoreCase: true);
    }

    [Fact]
    public void Fallback_MatchesDapperAotResults()
    {
        var store = new ProbeStoreFallback(_factory);
        store.CreateSchema();
        foreach (var r in SampleRows()) store.Insert(r);
        Assert.Equal(3L, store.Count());
        var hits = store.Search("harbor");
        Assert.Single(hits);
        Assert.Equal("Harbor status", hits[0].Title);
    }
}
