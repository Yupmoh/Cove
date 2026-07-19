using Cove.Persistence;
using Cove.Testing;
using Xunit;

namespace Cove.Persistence.Tests;

public sealed class SqliteConnectionFactorySidecarTests : IDisposable
{
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly string _directory;
    private readonly string _databasePath;

    public SqliteConnectionFactorySidecarTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"cove-sqlite-sidecar-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _databasePath = Path.Combine(_directory, "store.db");
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        TestDirectory.Delete(_directory);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void Open_SetsOwnerOnlyModeOnMainDatabase()
    {
        var factory = new SqliteConnectionFactory(_databasePath);
        using var connection = factory.Open();

        Assert.Equal(OwnerOnly, File.GetUnixFileMode(_databasePath));
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void Open_AfterWalWrite_SetsOwnerOnlyModeOnWalAndShmSidecars()
    {
        var factory = new SqliteConnectionFactory(_databasePath);
        using var connection = factory.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE entries (id INTEGER PRIMARY KEY); INSERT INTO entries DEFAULT VALUES;";
        command.ExecuteNonQuery();

        var walPath = _databasePath + "-wal";
        var shmPath = _databasePath + "-shm";
        Assert.True(File.Exists(walPath));
        Assert.True(File.Exists(shmPath));
        Assert.Equal(OwnerOnly, File.GetUnixFileMode(walPath));
        Assert.Equal(OwnerOnly, File.GetUnixFileMode(shmPath));
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void Open_SetsOwnerOnlyModeOnEveryExistingDatabaseSidecar()
    {
        var sidecars = new[]
        {
            _databasePath + "-journal",
            _databasePath + "-wal",
            _databasePath + "-shm",
        };
        foreach (var sidecar in sidecars)
        {
            File.WriteAllText(sidecar, string.Empty);
            File.SetUnixFileMode(
                sidecar,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }

        var durability = new RecordingFileDurability();
        var factory = new SqliteConnectionFactory(_databasePath, durability: durability);
        using var connection = factory.Open();

        foreach (var sidecar in sidecars)
            Assert.Contains(sidecar, durability.OwnerOnlyPaths);
    }

    [Fact]
    public void Open_AppliesCanonicalConnectionPolicy()
    {
        var factory = new SqliteConnectionFactory(_databasePath);
        using var connection = factory.Open();

        Assert.Equal("wal", Scalar(connection, "PRAGMA journal_mode"));
        Assert.Equal(1L, Scalar(connection, "PRAGMA synchronous"));
        Assert.Equal(5000L, Scalar(connection, "PRAGMA busy_timeout"));
        Assert.Equal(1L, Scalar(connection, "PRAGMA foreign_keys"));
        Assert.Equal(2L, Scalar(connection, "PRAGMA temp_store"));
        Assert.Equal(1000L, Scalar(connection, "PRAGMA wal_autocheckpoint"));
    }

    [Fact]
    public void OpenReadOnlyImport_UsesReadOnlyQueryPolicyWithoutMutatingSource()
    {
        using (var fixture = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_databasePath}"))
        {
            fixture.Open();
            using var create = fixture.CreateCommand();
            create.CommandText = "CREATE TABLE entries (id INTEGER PRIMARY KEY); INSERT INTO entries DEFAULT VALUES;";
            create.ExecuteNonQuery();
        }

        var originalWriteTime = File.GetLastWriteTimeUtc(_databasePath);
        var factory = SqliteConnectionFactory.CreateReadOnlyImport(_databasePath);
        using var connection = factory.Open();
        using var policy = connection.CreateCommand();
        policy.CommandText = """
            SELECT
                (SELECT timeout FROM pragma_busy_timeout),
                (SELECT foreign_keys FROM pragma_foreign_keys),
                (SELECT query_only FROM pragma_query_only)
            """;
        using var reader = policy.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal(5000L, reader.GetInt64(0));
        Assert.Equal(1L, reader.GetInt64(1));
        Assert.Equal(1L, reader.GetInt64(2));
        Assert.Equal(originalWriteTime, File.GetLastWriteTimeUtc(_databasePath));
        Assert.False(File.Exists(_databasePath + "-wal"));
        Assert.False(File.Exists(_databasePath + "-shm"));
        using var write = connection.CreateCommand();
        write.CommandText = "INSERT INTO entries DEFAULT VALUES";
        var exception = Record.Exception(() => { write.ExecuteNonQuery(); });
        Assert.IsType<Microsoft.Data.Sqlite.SqliteException>(exception);
    }

    private sealed class RecordingFileDurability : Cove.Platform.IFileDurability
    {
        public List<string> OwnerOnlyPaths { get; } = [];

        public void SetOwnerOnly(string path, Microsoft.Extensions.Logging.ILogger? logger = null)
            => OwnerOnlyPaths.Add(path);

        public void FlushDirectory(string path, Microsoft.Extensions.Logging.ILogger? logger = null)
        {
        }
    }

    private static object Scalar(Microsoft.Data.Sqlite.SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar()!;
    }
}
