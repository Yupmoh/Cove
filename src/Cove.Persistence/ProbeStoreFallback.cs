using Microsoft.Data.Sqlite;

namespace Cove.Persistence;

public sealed class ProbeStoreFallback
{
    private readonly SqliteConnectionFactory _factory;

    public ProbeStoreFallback(SqliteConnectionFactory factory) => _factory = factory;

    public void CreateSchema()
    {
        using var conn = _factory.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = ProbeSchema.Ddl;
        cmd.ExecuteNonQuery();
    }

    public void Insert(ProbeRow row)
    {
        using var conn = _factory.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO probe(id, created_at, title, body) VALUES ($id, $created, $title, $body)";
        cmd.Parameters.AddWithValue("$id", row.Id);
        cmd.Parameters.AddWithValue("$created", row.CreatedAt);
        cmd.Parameters.AddWithValue("$title", row.Title);
        cmd.Parameters.AddWithValue("$body", row.Body);
        cmd.ExecuteNonQuery();
    }

    public long Count()
    {
        using var conn = _factory.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM probe";
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }

    public IReadOnlyList<ProbeHit> Search(string query)
    {
        using var conn = _factory.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT p.id, p.title " +
            "FROM probe_fts f JOIN probe p ON p.rowid = f.rowid " +
            "WHERE probe_fts MATCH $q ORDER BY rank";
        cmd.Parameters.AddWithValue("$q", query);
        using var reader = cmd.ExecuteReader();
        var hits = new List<ProbeHit>();
        while (reader.Read())
            hits.Add(new ProbeHit(reader.GetString(0), reader.GetString(1)));
        return hits;
    }
}
