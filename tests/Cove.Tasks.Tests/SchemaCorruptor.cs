using Cove.Persistence;
using Microsoft.Data.Sqlite;

namespace Cove.Tasks.Tests;

internal static class SchemaCorruptor
{
    public static void DropColumn(SqliteConnectionFactory factory, string table, string column)
    {
        using var conn = factory.Open();
        var cols = new System.Collections.Generic.List<string>();
        using (var info = conn.CreateCommand())
        {
            info.CommandText = $"PRAGMA table_info({table})";
            using var reader = info.ExecuteReader();
            while (reader.Read())
                cols.Add(reader.GetString(1));
        }
        if (!cols.Contains(column))
            return;
        var keep = cols.Where(c => c != column).Select(c => $"\"{c}\"").ToArray();
        var temp = table + "_rebuild";
        using (var drop = conn.CreateCommand()) { drop.CommandText = $"DROP TABLE IF EXISTS {temp}"; drop.ExecuteNonQuery(); }
        using (var create = conn.CreateCommand()) { create.CommandText = $"CREATE TABLE {temp} AS SELECT {string.Join(", ", keep)} FROM {table}"; create.ExecuteNonQuery(); }
        using (var dropOld = conn.CreateCommand()) { dropOld.CommandText = $"DROP TABLE {table}"; dropOld.ExecuteNonQuery(); }
        using (var rename = conn.CreateCommand()) { rename.CommandText = $"ALTER TABLE {temp} RENAME TO {table}"; rename.ExecuteNonQuery(); }
    }

    public static void ChangeColumnType(SqliteConnectionFactory factory, string table, string column, string newType)
    {
        using var conn = factory.Open();
        using (var cmd = conn.CreateCommand()) { cmd.CommandText = $"ALTER TABLE {table} RENAME COLUMN {column} TO {column}_old"; cmd.ExecuteNonQuery(); }
        using (var add = conn.CreateCommand()) { add.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {newType}"; add.ExecuteNonQuery(); }
        using (var copy = conn.CreateCommand()) { copy.CommandText = $"UPDATE {table} SET {column} = {column}_old"; copy.ExecuteNonQuery(); }
        using (var drop = conn.CreateCommand()) { drop.CommandText = $"ALTER TABLE {table} DROP COLUMN {column}_old"; drop.ExecuteNonQuery(); }
    }
}
