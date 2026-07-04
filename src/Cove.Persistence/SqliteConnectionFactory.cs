using Microsoft.Data.Sqlite;

namespace Cove.Persistence;

public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;
    private readonly string _databasePath;

    public SqliteConnectionFactory(string databasePath)
    {
        SqliteBootstrap.EnsureInitialized();
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default,
            Pooling = true,
        }.ToString();
        _databasePath = databasePath;
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText =
            "PRAGMA journal_mode=WAL;" +
            "PRAGMA synchronous=NORMAL;" +
            "PRAGMA busy_timeout=5000;" +
            "PRAGMA foreign_keys=ON;" +
            "PRAGMA temp_store=MEMORY;" +
            "PRAGMA wal_autocheckpoint=1000;";
        pragma.ExecuteNonQuery();

        ApplyOwnerOnlyMode();
        return connection;
    }

    private void ApplyOwnerOnlyMode()
    {
        if (OperatingSystem.IsWindows()) return;
        if (!File.Exists(_databasePath)) return;
        File.SetUnixFileMode(_databasePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
