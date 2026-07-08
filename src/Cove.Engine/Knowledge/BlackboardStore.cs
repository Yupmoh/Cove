using Cove.Protocol;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed class BlackboardStore
{
    private readonly string _dbPath;
    private readonly ILogger _logger;

    public BlackboardStore(string dataDir, ILogger logger)
    {
        _dbPath = System.IO.Path.Combine(dataDir, "memory", "memory.db");
        _logger = logger;
    }

    public BlackboardPost Post(string workspaceId, string kind, string audience, string content, string? refId = null, System.TimeSpan? ttl = null)
    {
        var post = new BlackboardPost
        {
            Id = System.Guid.NewGuid().ToString("N"),
            WorkspaceId = workspaceId,
            Kind = kind,
            Audience = audience,
            Content = content,
            RefId = refId,
            CreatedAt = System.DateTimeOffset.UtcNow,
            ExpiresAt = ttl.HasValue ? System.DateTimeOffset.UtcNow.Add(ttl.Value) : null,
        };

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO blackboard (id, workspace_id, kind, audience, content, ref_id, ttl_at, created_at)
            VALUES (@id, @ws, @kind, @aud, @content, @ref, @ttl, @created);
            """;
        cmd.Parameters.AddWithValue("@id", post.Id);
        cmd.Parameters.AddWithValue("@ws", post.WorkspaceId);
        cmd.Parameters.AddWithValue("@kind", post.Kind);
        cmd.Parameters.AddWithValue("@aud", post.Audience);
        cmd.Parameters.AddWithValue("@content", post.Content);
        cmd.Parameters.AddWithValue("@ref", (object?)post.RefId ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@ttl", (object?)post.ExpiresAt?.ToString("o") ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@created", post.CreatedAt.ToString("o"));
        cmd.ExecuteNonQuery();

        _logger.LogWarning("blackboard: posted {kind} for {ws} → {aud}", post.Kind, post.WorkspaceId, post.Audience);
        return post;
    }

    public System.Collections.Generic.IReadOnlyList<BlackboardPost> Show(string workspaceId, string? audience = null)
    {
        SweepExpired(workspaceId);

        var result = new System.Collections.Generic.List<BlackboardPost>();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        if (audience is null)
        {
            cmd.CommandText = "SELECT id, workspace_id, kind, audience, content, ref_id, ttl_at, created_at FROM blackboard WHERE workspace_id = @ws ORDER BY created_at DESC";
            cmd.Parameters.AddWithValue("@ws", workspaceId);
        }
        else
        {
            cmd.CommandText = "SELECT id, workspace_id, kind, audience, content, ref_id, ttl_at, created_at FROM blackboard WHERE workspace_id = @ws AND audience = @aud ORDER BY created_at DESC";
            cmd.Parameters.AddWithValue("@ws", workspaceId);
            cmd.Parameters.AddWithValue("@aud", audience);
        }
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(ReadPost(reader));
        }
        return result;
    }

    private void SweepExpired(string workspaceId)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM blackboard WHERE workspace_id = @ws AND ttl_at IS NOT NULL AND ttl_at < @now";
        cmd.Parameters.AddWithValue("@ws", workspaceId);
        cmd.Parameters.AddWithValue("@now", System.DateTimeOffset.UtcNow.ToString("o"));
        var deleted = cmd.ExecuteNonQuery();
        if (deleted > 0)
            _logger.LogWarning("blackboard: swept {count} expired posts for {ws}", deleted, workspaceId);
    }

    private static BlackboardPost ReadPost(SqliteDataReader reader)
    {
        return new BlackboardPost
        {
            Id = reader.GetString(0),
            WorkspaceId = reader.GetString(1),
            Kind = reader.GetString(2),
            Audience = reader.GetString(3),
            Content = reader.GetString(4),
            RefId = reader.IsDBNull(5) ? null : reader.GetString(5),
            ExpiresAt = reader.IsDBNull(6) ? null : System.DateTimeOffset.Parse(reader.GetString(6), null, System.Globalization.DateTimeStyles.RoundtripKind),
            CreatedAt = System.DateTimeOffset.Parse(reader.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind),
        };
    }
}
