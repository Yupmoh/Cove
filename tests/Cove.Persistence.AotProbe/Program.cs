using Cove.Persistence;

var dbPath = Path.Combine(Path.GetTempPath(), $"cove-aotprobe-{Guid.NewGuid():N}.db");

int exitCode = 0;
try
{
    SqliteBootstrap.EnsureInitialized();
    var factory = new SqliteConnectionFactory(dbPath);

    long count;
    string journalMode;
    IReadOnlyList<ProbeHit> hits;

    var rows = new[]
    {
        new ProbeRow(Guid.CreateVersion7().ToString("D"),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            "Harbor status", "the deep teal harbor is calm tonight"),
        new ProbeRow(Guid.CreateVersion7().ToString("D"),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            "Build log", "native aot publish succeeded on three rids"),
        new ProbeRow(Guid.CreateVersion7().ToString("D"),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            "Notes", "flat json state written atomically"),
    };

    var store = new ProbeStoreFallback(factory);
    store.CreateSchema();
    foreach (var r in rows) store.Insert(r);
    count = store.Count();
    hits = store.Search("harbor");

    using (var conn = factory.Open())
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "PRAGMA journal_mode;";
        journalMode = (string)(cmd.ExecuteScalar() ?? "unknown");
    }

    if (count != 3)
        throw new Exception($"expected 3 rows, got {count}");
    if (!string.Equals(journalMode, "wal", StringComparison.OrdinalIgnoreCase))
        throw new Exception($"expected journal_mode=wal, got {journalMode}");
    if (hits.Count != 1)
        throw new Exception($"expected 1 fts hit for 'harbor', got {hits.Count}");

    Console.WriteLine(
        $"PROBE OK mode=fallback " +
        $"journal_mode={journalMode} rows={count} fts5_hits={hits.Count}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"PROBE FAIL: {ex.GetType().Name}: {ex.Message}");
    exitCode = 1;
}
finally
{
    try
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = dbPath + suffix;
            if (File.Exists(path))
                File.Delete(path);
            if (File.Exists(path))
                throw new IOException($"probe artifact remains after cleanup: {path}");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"PROBE CLEANUP FAIL: {ex.GetType().Name}: {ex.Message}");
        exitCode = 1;
    }
}

return exitCode;
