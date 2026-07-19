using Cove.Persistence;
using Cove.Tasks.Export;
using Cove.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Tasks.Tests;

public sealed class TaskBoardExportServiceTests
{
    [Fact]
    public void Export_UsesRepositoryContract()
    {
        var repository = new FakeExportRepository
        {
            ExportResult = new RepositoryExportResult(true, null),
        };
        var service = new TaskBoardExportService(repository, NullLogger.Instance);

        var result = service.Export("/tmp/tasks-export.db", 3);

        Assert.True(result.Success);
        Assert.Equal("/tmp/tasks-export.db", repository.ExportPath);
        Assert.Equal(3, result.Manifest!.BayCount);
    }

    [Fact]
    public void DiffAgainst_UsesRepositoryContract()
    {
        var expected = new[] { new RowDiff("cards", "card-1", "added", null, null) };
        var repository = new FakeExportRepository
        {
            Diffs = expected,
        };
        var service = new TaskBoardExportService(repository, NullLogger.Instance);

        var result = service.DiffAgainst("/tmp/tasks-import.db");

        Assert.True(result.Success);
        Assert.Same(expected, result.Diffs);
        Assert.Equal("/tmp/tasks-import.db", repository.ImportPath);
    }

    [Fact]
    public void SqliteRepository_DiffReadsImportWithoutCreatingWritableSidecars()
    {
        var directory = TestDirectory.Create("cove-task-export");
        try
        {
            var currentPath = Path.Combine(directory, "current.db");
            var importPath = Path.Combine(directory, "import.db");
            CreateDiffFixture(currentPath, cardId: null);
            CreateDiffFixture(importPath, cardId: "imported-card");
            var originalWriteTime = File.GetLastWriteTimeUtc(importPath);
            var repository = new SqliteTaskBoardExportRepository(new SqliteConnectionFactory(currentPath));

            var diffs = repository.DiffAgainst(importPath);

            Assert.Contains(diffs, diff => diff is { Table: "cards", Id: "imported-card", ChangeType: "added" });
            Assert.Equal(originalWriteTime, File.GetLastWriteTimeUtc(importPath));
            Assert.False(File.Exists(importPath + "-wal"));
            Assert.False(File.Exists(importPath + "-shm"));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            TestDirectory.Delete(directory);
        }
    }

    private static void CreateDiffFixture(string path, string? cardId)
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE cards (id TEXT PRIMARY KEY);
            CREATE TABLE statuses (id TEXT PRIMARY KEY);
            CREATE TABLE labels (id TEXT PRIMARY KEY);
            CREATE TABLE comments (id TEXT PRIMARY KEY);
            CREATE TABLE task_runs (id TEXT PRIMARY KEY);
            CREATE TABLE task_run_segments (id TEXT PRIMARY KEY);
            CREATE TABLE card_schedules (card_id TEXT PRIMARY KEY);
            """;
        command.ExecuteNonQuery();
        if (cardId is null)
            return;

        command.CommandText = "INSERT INTO cards (id) VALUES (@id)";
        command.Parameters.AddWithValue("@id", cardId);
        command.ExecuteNonQuery();
    }

    private sealed class FakeExportRepository : ITaskBoardExportRepository
    {
        public RepositoryExportResult ExportResult { get; init; } = new(true, null);
        public IReadOnlyList<RowDiff> Diffs { get; init; } = [];
        public string? ExportPath { get; private set; }
        public string? ImportPath { get; private set; }

        public RepositoryExportResult Export(string exportPath)
        {
            ExportPath = exportPath;
            return ExportResult;
        }

        public IReadOnlyList<RowDiff> DiffAgainst(string importPath)
        {
            ImportPath = importPath;
            return Diffs;
        }
    }
}
