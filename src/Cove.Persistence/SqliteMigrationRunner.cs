using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Persistence;

public sealed class SqliteMigration
{
    public required int Version { get; init; }
    public required string Sql { get; init; }
}

public static class SqliteMigrationRunner
{
    public static int Apply(SqliteConnection connection, IReadOnlyList<SqliteMigration> migrations, ILogger? logger = null)
    {
        var ordered = new List<SqliteMigration>(migrations);
        ordered.Sort(static (a, b) => a.Version.CompareTo(b.Version));

        long current;
        using (var read = connection.CreateCommand())
        {
            read.CommandText = "PRAGMA user_version;";
            current = Convert.ToInt64(read.ExecuteScalar() ?? 0L);
        }

        var startVersion = current;
        int applied = 0;
        foreach (var migration in ordered)
        {
            if (migration.Version <= current)
                continue;

            using var transaction = connection.BeginTransaction();
            using (var apply = connection.CreateCommand())
            {
                apply.Transaction = transaction;
                apply.CommandText = migration.Sql;
                apply.ExecuteNonQuery();
            }
            using (var bump = connection.CreateCommand())
            {
                bump.Transaction = transaction;
                bump.CommandText = $"PRAGMA user_version = {migration.Version};";
                bump.ExecuteNonQuery();
            }
            transaction.Commit();
            current = migration.Version;
            applied++;
            logger?.SqliteMigrationApplied(migration.Version);
        }

        logger?.SqliteMigrationsComplete(startVersion, current, applied);
        return applied;
    }
}
