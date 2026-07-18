using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Persistence;

public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;
    private readonly string _databasePath;
    private readonly string _walPath;
    private readonly string _sharedMemoryPath;
    private readonly ILogger _logger;

    public SqliteConnectionFactory(string databasePath, ILogger? logger = null)
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
        _walPath = databasePath + "-wal";
        _sharedMemoryPath = databasePath + "-shm";
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
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

        ApplyOwnerOnlyMode(_databasePath);
        ApplyOwnerOnlyMode(_walPath);
        ApplyOwnerOnlyMode(_sharedMemoryPath);
        return connection;
    }

    private void ApplyOwnerOnlyMode(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        if (!File.Exists(path)) return;
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception ex)
        {
            _logger.SqliteChmodFailed(path, ex.Message);
        }
    }
}

internal static partial class SqliteConnectionFactoryLog
{
    [ZLoggerMessage(LogLevel.Warning, "sqlite chmod failed path={path} error={error}")]
    public static partial void SqliteChmodFailed(this ILogger logger, string path, string error);
}
