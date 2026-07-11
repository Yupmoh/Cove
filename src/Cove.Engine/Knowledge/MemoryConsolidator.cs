using System.Threading;
using Cove.Protocol;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;


public sealed class ProposalStore
{
    private readonly string _dbPath;
    private readonly ILogger _logger;

    public ProposalStore(string dataDir, ILogger logger)
    {
        _dbPath = System.IO.Path.Combine(dataDir, "memory", "memory.db");
        _logger = logger;
    }

    public Proposal Create(string bayId, string kind, string content)
    {
        var proposal = new Proposal(
            System.Guid.NewGuid().ToString("N"),
            bayId,
            kind,
            content,
            "proposed",
            System.DateTimeOffset.UtcNow
        );

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        EnsureTable(conn);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO proposals (id, bay_id, kind, content, state, created_at) VALUES (@id, @ws, @kind, @content, @state, @ts)";
        cmd.Parameters.AddWithValue("@id", proposal.Id);
        cmd.Parameters.AddWithValue("@ws", proposal.BayId);
        cmd.Parameters.AddWithValue("@kind", proposal.Kind);
        cmd.Parameters.AddWithValue("@content", proposal.Content);
        cmd.Parameters.AddWithValue("@state", proposal.State);
        cmd.Parameters.AddWithValue("@ts", proposal.CreatedAt.ToString("o"));
        cmd.ExecuteNonQuery();

        _logger.LogWarning("proposals: created {id} ({kind}) in {ws}", proposal.Id, proposal.Kind, proposal.BayId);
        return proposal;
    }

    public Proposal? Get(string id)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        EnsureTable(conn);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, bay_id, kind, content, state, created_at FROM proposals WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return ReadProposal(reader);
    }

    public System.Collections.Generic.IReadOnlyList<Proposal> ListByBay(string bayId, string? state = null)
    {
        var result = new System.Collections.Generic.List<Proposal>();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        EnsureTable(conn);
        using var cmd = conn.CreateCommand();
        if (state is null)
        {
            cmd.CommandText = "SELECT id, bay_id, kind, content, state, created_at FROM proposals WHERE bay_id = @ws ORDER BY created_at DESC";
            cmd.Parameters.AddWithValue("@ws", bayId);
        }
        else
        {
            cmd.CommandText = "SELECT id, bay_id, kind, content, state, created_at FROM proposals WHERE bay_id = @ws AND state = @state ORDER BY created_at DESC";
            cmd.Parameters.AddWithValue("@ws", bayId);
            cmd.Parameters.AddWithValue("@state", state);
        }
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(ReadProposal(reader));
        return result;
    }

    public bool Transition(string id, string newState)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        EnsureTable(conn);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE proposals SET state = @state WHERE id = @id AND state != @state";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@state", newState);
        var rows = cmd.ExecuteNonQuery();
        if (rows > 0)
            _logger.LogWarning("proposals: transitioned {id} → {state}", id, newState);
        return rows > 0;
    }

    private static void EnsureTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS proposals (
                id TEXT PRIMARY KEY,
                bay_id TEXT NOT NULL,
                kind TEXT NOT NULL,
                content TEXT NOT NULL,
                state TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static Proposal ReadProposal(SqliteDataReader reader)
    {
        return new Proposal(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            System.DateTimeOffset.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind)
        );
    }
}

public sealed class MemoryConsolidator
{
    private readonly MemoryStore _store;
    private readonly ProposalStore _proposals;
    private readonly ILogger _logger;
    private CancellationTokenSource? _cts;

    public MemoryConsolidator(MemoryStore store, ProposalStore proposals, ILogger logger)
    {
        _store = store;
        _proposals = proposals;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task<int> ConsolidateAsync(string bayId, bool dryRun, CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cts.Token;

        var facts = _store.ListFacts(bayId);
        _logger.LogWarning("consolidator: analyzing {count} facts in {ws} (dryRun={dry})", facts.Count, bayId, dryRun);

        int proposalCount = 0;
        var seen = new System.Collections.Generic.HashSet<string>();

        foreach (var fact in facts)
        {
            token.ThrowIfCancellationRequested();

            var key = fact.Kind + ":" + fact.Content.ToLowerInvariant();
            if (seen.Contains(key))
            {
                if (!dryRun)
                {
                    var proposal = _proposals.Create(bayId, "merge", $"Merge duplicate fact: {fact.Content}");
                    _logger.LogWarning("consolidator: proposed merge {id} for fact {factId}", proposal.Id, fact.Id);
                    proposalCount++;
                }
                else
                {
                    proposalCount++;
                }
            }
            seen.Add(key);
        }

        await System.Threading.Tasks.Task.CompletedTask;
        _logger.LogWarning("consolidator: found {count} consolidation candidates in {ws}", proposalCount, bayId);
        return proposalCount;
    }

    public void Cancel()
    {
        _cts?.Cancel();
        _logger.LogWarning("consolidator: cancellation requested");
    }
}
