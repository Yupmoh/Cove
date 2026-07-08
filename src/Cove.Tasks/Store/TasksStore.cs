using System.Data;
using Cove.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Tasks.Store;

public sealed class TasksStore
{
    private readonly SqliteConnectionFactory _factory;
    private readonly ILogger _logger;
    private readonly object _bootLock = new();
    private int _bootstrapped;

    public TasksStore(SqliteConnectionFactory factory, ILogger logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public void EnsureSchema()
    {
        lock (_bootLock)
        {
            if (_bootstrapped == 1)
                return;
            using var conn = _factory.Open();
            SqliteMigrationRunner.Apply(conn, TasksSchema.Migrations);
            SelfHeal(conn);
            _bootstrapped = 1;
        }
    }

    private void SelfHeal(SqliteConnection conn)
    {
        foreach (var (table, columns) in ExpectedColumns.All)
        {
            var present = ListColumnNames(conn, table);
            foreach (var expected in columns)
            {
                if (!present.Contains(expected.Name))
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {expected.Name} {expected.DeclaredType}";
                    cmd.ExecuteNonQuery();
                    _logger.SelfHealAddedColumn(table, expected.Name, expected.DeclaredType);
                }
            }
        }
        foreach (var (table, columns) in ExpectedColumns.All)
        {
            foreach (var expected in columns)
            {
                var declared = GetColumnDeclaredType(conn, table, expected.Name);
                if (!string.Equals(declared, expected.DeclaredType, System.StringComparison.OrdinalIgnoreCase))
                {
                    _logger.SelfHealNonAdditiveChange(table, expected.Name, declared, expected.DeclaredType);
                }
            }
        }
    }

    public int GetUserVersion()
    {
        EnsureSchema();
        using var conn = _factory.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    public IReadOnlyList<string> ListTableNames()
    {
        EnsureSchema();
        using var conn = _factory.Open();
        return ListTableNames(conn);
    }

    private static IReadOnlyList<string> ListTableNames(SqliteConnection conn)
    {
        var result = new System.Collections.Generic.List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(reader.GetString(0));
        return result;
    }

    public IReadOnlyList<string> ListColumnNames(string table)
    {
        EnsureSchema();
        using var conn = _factory.Open();
        return ListColumnNames(conn, table);
    }

    private static IReadOnlyList<string> ListColumnNames(SqliteConnection conn, string table)
    {
        var result = new System.Collections.Generic.List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(reader.GetString(1));
        return result;
    }

    public string GetColumnDeclaredType(string table, string column)
    {
        EnsureSchema();
        using var conn = _factory.Open();
        return GetColumnDeclaredType(conn, table, column);
    }

    private static string GetColumnDeclaredType(SqliteConnection conn, string table, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1) == column)
                return reader.GetString(2);
        }
        return "";
    }
}
