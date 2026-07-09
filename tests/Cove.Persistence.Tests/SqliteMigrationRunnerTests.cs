using Cove.Persistence;
using Xunit;

namespace Cove.Persistence.Tests;

public sealed class SqliteMigrationRunnerTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;

    public SqliteMigrationRunnerTests()
    {
        SqliteBootstrap.EnsureInitialized();
        _dbPath = Path.Combine(Path.GetTempPath(), $"cove-migrate-test-{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory(_dbPath);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _dbPath + suffix;
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static readonly SqliteMigration[] TwoMigrations =
    [
        new() { Version = 1, Sql = "CREATE TABLE a (id INTEGER PRIMARY KEY);" },
        new() { Version = 2, Sql = "CREATE TABLE b (id INTEGER PRIMARY KEY);" },
    ];

    [Fact]
    public void Apply_RunsPendingMigrations_AndBumpsUserVersion()
    {
        using var connection = _factory.Open();
        int applied = SqliteMigrationRunner.Apply(connection, TwoMigrations);

        Assert.Equal(2, applied);

        using var version = connection.CreateCommand();
        version.CommandText = "PRAGMA user_version;";
        Assert.Equal(2L, Convert.ToInt64(version.ExecuteScalar()));

        using var tables = connection.CreateCommand();
        tables.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name IN ('a','b');";
        Assert.Equal(2L, Convert.ToInt64(tables.ExecuteScalar()));
    }

    [Fact]
    public void Apply_IsIdempotent_SkipsAppliedVersions()
    {
        using var connection = _factory.Open();
        SqliteMigrationRunner.Apply(connection, TwoMigrations);
        int second = SqliteMigrationRunner.Apply(connection, TwoMigrations);
        Assert.Equal(0, second);
    }

    [Fact]
    public void Apply_OnlyRunsNewerVersions_WhenPartiallyApplied()
    {
        using var connection = _factory.Open();
        SqliteMigrationRunner.Apply(connection, [TwoMigrations[0]]);

        int applied = SqliteMigrationRunner.Apply(connection, TwoMigrations);
        Assert.Equal(1, applied);
    }

    [Fact]
    public void Apply_FtsConvention_CreatesSearchableTable()
    {
        using var connection = _factory.Open();
        var migration = new SqliteMigration
        {
            Version = 1,
            Sql =
                $"CREATE VIRTUAL TABLE docs USING fts5(body, tokenize='{SqliteFts.Tokenizer}', prefix='{SqliteFts.PrefixIndex}');" +
                "INSERT INTO docs(body) VALUES ('the running daemon owns terminals');",
        };
        SqliteMigrationRunner.Apply(connection, [migration]);

        using var query = connection.CreateCommand();
        query.CommandText = "SELECT count(*) FROM docs WHERE docs MATCH 'run';";
        Assert.Equal(1L, Convert.ToInt64(query.ExecuteScalar()));
    }
}
