using Cove.Persistence;
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
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Fact]
    public void Open_SetsOwnerOnlyModeOnMainDatabase()
    {
        if (OperatingSystem.IsWindows()) return;

        var factory = new SqliteConnectionFactory(_databasePath);
        using var connection = factory.Open();

        Assert.Equal(OwnerOnly, File.GetUnixFileMode(_databasePath));
    }

    [Fact]
    public void Open_AfterWalWrite_SetsOwnerOnlyModeOnWalAndShmSidecars()
    {
        if (OperatingSystem.IsWindows()) return;

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
}
