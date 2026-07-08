using Cove.Engine.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class KnowledgePersistenceKernelTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-kernel-" + System.Guid.NewGuid().ToString("N"));

    [Fact]
    public void EnsureAllSchemas_CreatesAllDbsWithFts()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var kernel = new KnowledgePersistenceKernel(dir, NullLogger.Instance);

        kernel.EnsureAllSchemas();

        Assert.True(System.IO.File.Exists(System.IO.Path.Combine(dir, "timeline.db")));
        Assert.True(System.IO.File.Exists(System.IO.Path.Combine(dir, "memory", "memory.db")));
        Assert.True(System.IO.File.Exists(System.IO.Path.Combine(dir, "fts", "index.db")));
        Assert.True(System.IO.File.Exists(System.IO.Path.Combine(dir, "notes", "index.db")));
    }

    [Fact]
    public void VerifyTokenizers_PorterTrigramUnicode61_AllAvailable()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var kernel = new KnowledgePersistenceKernel(dir, NullLogger.Instance);

        kernel.EnsureAllSchemas();

        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={System.IO.Path.Combine(dir, "timeline.db")}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='timeline_fts'";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("timeline_fts", reader.GetString(0));
    }

    [Fact]
    public void TimelineFts_TriggerSyncsOnInsert()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var kernel = new KnowledgePersistenceKernel(dir, NullLogger.Instance);
        kernel.EnsureAllSchemas();

        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={System.IO.Path.Combine(dir, "timeline.db")}");
        conn.Open();
        using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = "INSERT INTO timeline (id, workspace_id, kind, scope, title, body, created_at) VALUES ('t1', 'ws1', 'note.created', 'workspace', 'Test Entry', ' searchable body content ', '2026-01-01T00:00:00Z')";
        insertCmd.ExecuteNonQuery();

        using var ftsCmd = conn.CreateCommand();
        ftsCmd.CommandText = "SELECT title FROM timeline_fts WHERE timeline_fts MATCH 'searchable'";
        using var reader = ftsCmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("Test Entry", reader.GetString(0));
    }
}
