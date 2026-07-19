using Cove.Platform;
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
    private readonly string _rollbackJournalPath;
    private readonly ILogger _logger;
    private readonly IFileDurability _durability;
    private readonly bool _readOnlyImport;

    public SqliteConnectionFactory(
        string databasePath,
        ILogger? logger = null,
        IFileDurability? durability = null)
        : this(databasePath, readOnlyImport: false, logger, durability)
    {
    }

    private SqliteConnectionFactory(
        string databasePath,
        bool readOnlyImport,
        ILogger? logger,
        IFileDurability? durability)
    {
        SqliteBootstrap.EnsureInitialized();
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = readOnlyImport ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default,
            Pooling = !readOnlyImport,
        }.ToString();
        _databasePath = databasePath;
        _walPath = databasePath + "-wal";
        _sharedMemoryPath = databasePath + "-shm";
        _rollbackJournalPath = databasePath + "-journal";
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _durability = durability ?? FileDurability.System;
        _readOnlyImport = readOnlyImport;
    }

    public static SqliteConnectionFactory CreateReadOnlyImport(
        string databasePath,
        ILogger? logger = null)
        => new(databasePath, readOnlyImport: true, logger, durability: null);

    public static void ProtectOwnedDatabaseFiles(
        string databasePath,
        ILogger? logger = null,
        IFileDurability? durability = null)
        => new SqliteConnectionFactory(databasePath, logger, durability)
            .ApplyExistingOwnerOnlyModes();

    public SqliteConnection Open()
    {
        if (!_readOnlyImport)
            ApplyExistingOwnerOnlyModes();
        var connection = new SqliteConnection(_connectionString);
        try
        {
            connection.Open();

            using var pragma = connection.CreateCommand();
            pragma.CommandText = _readOnlyImport
                ? "PRAGMA busy_timeout=5000;" +
                  "PRAGMA foreign_keys=ON;" +
                  "PRAGMA query_only=ON;" +
                  "PRAGMA temp_store=MEMORY;"
                : "PRAGMA journal_mode=WAL;" +
                  "PRAGMA synchronous=NORMAL;" +
                  "PRAGMA busy_timeout=5000;" +
                  "PRAGMA foreign_keys=ON;" +
                  "PRAGMA temp_store=MEMORY;" +
                  "PRAGMA wal_autocheckpoint=1000;";
            pragma.ExecuteNonQuery();

            if (!_readOnlyImport)
                ApplyExistingOwnerOnlyModes();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public void ApplyExistingOwnerOnlyModes()
    {
        ApplyOwnerOnlyMode(_databasePath);
        ApplyOwnerOnlyMode(_walPath);
        ApplyOwnerOnlyMode(_sharedMemoryPath);
        ApplyOwnerOnlyMode(_rollbackJournalPath);
    }

    private void ApplyOwnerOnlyMode(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            _durability.SetOwnerOnly(path, _logger);
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
