using System.Text.Json;
using Cove.Protocol;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed record RankedFact(Fact Fact, double Score, string? Snippet);

public sealed record RecallPreview(string Id, string Kind, string Preview, double Score, string HowLongAgo);

public sealed class MemoryRanker
{
    private readonly MemoryStore _store;
    private readonly string _dbPath;
    private readonly ILogger _logger;

    public MemoryRanker(MemoryStore store, string dataDir, ILogger logger)
    {
        _store = store;
        _dbPath = System.IO.Path.Combine(dataDir, "memory", "memory.db");
        _logger = logger;
    }

    public System.Collections.Generic.IReadOnlyList<RankedFact> SearchRanked(string bayId, string query, int limit = 20)
    {
        var ftsResults = _store.SearchFacts(bayId, query, limit * 2);
        var result = new System.Collections.Generic.List<RankedFact>();

        foreach (var fact in ftsResults)
        {
            var bm25Score = ComputeBm25(bayId, query, fact);
            var hotness = ComputeHotness(fact);
            var fused = bm25Score * 0.6 + hotness * 0.4;
            var snippet = ExtractSnippet(fact.Content, query);
            result.Add(new RankedFact(fact, fused, snippet));
        }

        result.Sort((a, b) => b.Score.CompareTo(a.Score));
        if (result.Count > limit)
            result.RemoveRange(limit, result.Count - limit);

        _logger.LogWarning("memory-ranker: ranked {count} facts for query '{query}' in {ws}", result.Count, query, bayId);
        return result;
    }

    public System.Collections.Generic.IReadOnlyList<RecallPreview> Recall(string bayId, string query, int limit = 10)
    {
        var ranked = SearchRanked(bayId, query, limit);
        var result = new System.Collections.Generic.List<RecallPreview>();

        foreach (var r in ranked)
        {
            var preview = r.Snippet ?? (r.Fact.Content.Length > 120 ? r.Fact.Content[..120] + "..." : r.Fact.Content);
            var howLong = FormatHowLongAgo(r.Fact.UpdatedAt);
            result.Add(new RecallPreview(r.Fact.Id, r.Fact.Kind, preview, r.Score, howLong));
            _store.IncrementAccessCount(bayId, r.Fact.Id);
            LogFeedback(bayId, r.Fact.Id, query);
        }

        return result;
    }

    private double ComputeBm25(string bayId, string query, Fact fact)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT bm25(facts_fts) FROM facts_fts WHERE facts_fts MATCH @query AND rowid = (SELECT rowid FROM facts WHERE id = @id)";
        cmd.Parameters.AddWithValue("@query", query);
        cmd.Parameters.AddWithValue("@id", fact.Id);
        var result = cmd.ExecuteScalar();
        if (result is double bm25) return -bm25;
        if (result is long l) return -(double)l;
        return 0.5;
    }

    private static double ComputeHotness(Fact fact)
    {
        var confidenceWeight = fact.Confidence;
        var accessWeight = System.Math.Log10(fact.AccessCount + 1);
        var ageDays = (System.DateTimeOffset.UtcNow - fact.UpdatedAt).TotalDays;
        var recencyDecay = 1.0 / (1.0 + ageDays * 0.01);
        return (confidenceWeight + accessWeight) * recencyDecay;
    }

    private static string? ExtractSnippet(string content, string query, int radius = 50)
    {
        var idx = content.IndexOf(query, System.StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var start = System.Math.Max(0, idx - radius);
        var end = System.Math.Min(content.Length, idx + query.Length + radius);
        var snippet = content.Substring(start, end - start);
        if (start > 0) snippet = "..." + snippet;
        if (end < content.Length) snippet += "...";
        return snippet;
    }

    private static string FormatHowLongAgo(System.DateTimeOffset timestamp)
    {
        var delta = System.DateTimeOffset.UtcNow - timestamp;
        if (delta.TotalMinutes < 1) return "just now";
        if (delta.TotalHours < 1) return $"{(int)delta.TotalMinutes}m ago";
        if (delta.TotalDays < 1) return $"{(int)delta.TotalHours}h ago";
        if (delta.TotalDays < 30) return $"{(int)delta.TotalDays}d ago";
        return timestamp.ToString("yyyy-MM-dd");
    }

    private void LogFeedback(string bayId, string factId, string query)
    {
        try
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS feedback (
                    id TEXT PRIMARY KEY,
                    bay_id TEXT NOT NULL,
                    fact_id TEXT NOT NULL,
                    query TEXT NOT NULL,
                    recalled_at TEXT NOT NULL
                );
                """;
            cmd.ExecuteNonQuery();
            cmd.CommandText = "INSERT INTO feedback (id, bay_id, fact_id, query, recalled_at) VALUES (@id, @ws, @fid, @q, @ts)";
            cmd.Parameters.AddWithValue("@id", System.Guid.NewGuid().ToString("N"));
            cmd.Parameters.AddWithValue("@ws", bayId);
            cmd.Parameters.AddWithValue("@fid", factId);
            cmd.Parameters.AddWithValue("@q", query);
            cmd.Parameters.AddWithValue("@ts", System.DateTimeOffset.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning("memory-ranker: feedback log failed: {err}", ex.Message);
        }
    }
}
