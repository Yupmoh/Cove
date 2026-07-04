using Dapper;
using Microsoft.Data.Sqlite;

namespace Cove.Persistence;

public sealed class ProbeStore
{
    private readonly SqliteConnectionFactory _factory;

    public ProbeStore(SqliteConnectionFactory factory) => _factory = factory;

    public void CreateSchema()
    {
        using var conn = _factory.Open();
        conn.Execute(ProbeSchema.Ddl);
    }

    public void Insert(ProbeRow row)
    {
        using var conn = _factory.Open();
        conn.Execute(
            "INSERT INTO probe(id, created_at, title, body) VALUES (@Id, @CreatedAt, @Title, @Body)",
            row);
    }

    public long Count()
    {
        using var conn = _factory.Open();
        return conn.ExecuteScalar<long>("SELECT count(*) FROM probe");
    }

    public IReadOnlyList<ProbeHit> Search(string query)
    {
        using var conn = _factory.Open();
        var hits = conn.Query<ProbeHit>(
            "SELECT p.id AS Id, p.title AS Title " +
            "FROM probe_fts f JOIN probe p ON p.rowid = f.rowid " +
            "WHERE probe_fts MATCH @Q ORDER BY rank",
            new MatchArg(query));
        return hits.AsList();
    }
}
