using Cove.Engine.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class KnowledgePersistenceKernelTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-kernel-" + System.Guid.NewGuid().ToString("N"));

    [Fact]
    public void CorpusPolicies_DeclareOneCanonicalTruthSource()
    {
        Assert.Equal(KnowledgeCorpusTruth.Files, KnowledgeCorpusPolicy.Notes);
        Assert.Equal(KnowledgeCorpusTruth.Sqlite, KnowledgeCorpusPolicy.Timeline);
        Assert.Equal(KnowledgeCorpusTruth.Sqlite, KnowledgeCorpusPolicy.Memory);
        Assert.Equal(KnowledgeCorpusTruth.Sqlite, KnowledgeCorpusPolicy.Sessions);
        Assert.Equal(KnowledgeCorpusTruth.Sqlite, KnowledgeCorpusPolicy.Library);
        Assert.Equal(KnowledgeCorpusTruth.Sqlite, KnowledgeCorpusPolicy.Reviews);
        Assert.Equal(KnowledgeCorpusTruth.Sqlite, KnowledgeCorpusPolicy.Attribution);
    }

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
    public void EnsureAllSchemas_AppliesCanonicalPolicyToEveryKnowledgeDatabase()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var kernel = new KnowledgePersistenceKernel(dir, NullLogger.Instance);
        kernel.EnsureAllSchemas();

        foreach (var database in new[]
                 {
                     kernel.TimelineDatabase,
                     kernel.MemoryDatabase,
                     kernel.SessionIndexDatabase,
                     kernel.NotesIndexDatabase,
                 })
        {
            using var connection = database.Open();
            Assert.Equal("wal", ScalarText(connection, "PRAGMA journal_mode"));
            Assert.Equal(1L, Scalar(connection, "PRAGMA synchronous"));
            Assert.Equal(5000L, Scalar(connection, "PRAGMA busy_timeout"));
            Assert.Equal(1L, Scalar(connection, "PRAGMA foreign_keys"));
        }
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
        insertCmd.CommandText = "INSERT INTO timeline (id, bay_id, kind, scope, title, body, created_at) VALUES ('t1', 'ws1', 'note.created', 'bay', 'Test Entry', ' searchable body content ', '2026-01-01T00:00:00Z')";
        insertCmd.ExecuteNonQuery();

        using var ftsCmd = conn.CreateCommand();
        ftsCmd.CommandText = "SELECT title FROM timeline_fts WHERE timeline_fts MATCH 'searchable'";
        using var reader = ftsCmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("Test Entry", reader.GetString(0));
    }

    [Fact]
    public void EpisodesFts_TriggersKeepInsertUpdateDeleteConsistent()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var kernel = new KnowledgePersistenceKernel(dir, NullLogger.Instance);
        kernel.EnsureAllSchemas();

        using var conn = kernel.MemoryDatabase.Open();
        Execute(conn, "INSERT INTO episodes (id, bay_id, summary_l0, created_at) VALUES ('e1', 'ws1', 'alpha summary', '2026-01-01T00:00:00Z')");
        Assert.Equal(1L, Scalar(conn, "SELECT COUNT(*) FROM episodes_fts WHERE episodes_fts MATCH 'alpha'"));

        Execute(conn, "UPDATE episodes SET summary_l0 = 'beta summary' WHERE id = 'e1'");
        Assert.Equal(0L, Scalar(conn, "SELECT COUNT(*) FROM episodes_fts WHERE episodes_fts MATCH 'alpha'"));
        Assert.Equal(1L, Scalar(conn, "SELECT COUNT(*) FROM episodes_fts WHERE episodes_fts MATCH 'beta'"));

        Execute(conn, "DELETE FROM episodes WHERE id = 'e1'");
        Assert.Equal(0L, Scalar(conn, "SELECT COUNT(*) FROM episodes_fts WHERE episodes_fts MATCH 'beta'"));
    }

    [Fact]
    public void SessionFts_TriggersKeepBothIndexesConsistent()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var kernel = new KnowledgePersistenceKernel(dir, NullLogger.Instance);
        kernel.EnsureAllSchemas();

        using var conn = kernel.SessionIndexDatabase.Open();
        Execute(conn, "INSERT INTO sessions (id, bay_id, adapter, corpus, started_at) VALUES ('s1', 'ws1', 'claude-code', 'alpha claude corpus', '2026-01-01T00:00:00Z')");
        AssertSessionIndexes(conn, "claude", 1L);

        Execute(conn, "UPDATE sessions SET corpus = 'beta codex corpus' WHERE id = 's1'");
        AssertSessionIndexes(conn, "claude", 0L);
        AssertSessionIndexes(conn, "codex", 1L);

        Execute(conn, "DELETE FROM sessions WHERE id = 's1'");
        AssertSessionIndexes(conn, "codex", 0L);
    }

    private static void AssertSessionIndexes(Microsoft.Data.Sqlite.SqliteConnection connection, string query, long expected)
    {
        Assert.Equal(expected, Scalar(connection, $"SELECT COUNT(*) FROM sessions_fts WHERE sessions_fts MATCH '{query}'"));
        Assert.Equal(expected, Scalar(connection, $"SELECT COUNT(*) FROM sessions_fts_trigram WHERE sessions_fts_trigram MATCH '{query}'"));
    }

    private static void Execute(Microsoft.Data.Sqlite.SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long Scalar(Microsoft.Data.Sqlite.SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }

    private static string ScalarText(Microsoft.Data.Sqlite.SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (string)command.ExecuteScalar()!;
    }
}
