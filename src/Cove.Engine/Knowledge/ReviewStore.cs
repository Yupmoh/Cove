using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed record ReviewComment(
    string Id,
    string RootId,
    string? ParentId,
    string CommitSha,
    string FilePath,
    int Line,
    string Author,
    string Body,
    string State,
    System.DateTimeOffset CreatedAt,
    System.DateTimeOffset? OrphanedAt,
    string? HunkId,
    string? ContextHash);

public sealed record ReviewAuditEntry(
    string Id,
    string CommentId,
    string FromState,
    string ToState,
    string Actor,
    System.DateTimeOffset At,
    string? Note);

public sealed record SessionTelemetry(
    string SessionId,
    string Adapter,
    int FilesTouched);

public sealed class ReviewStore
{
    private readonly string _reviewsDir;
    private readonly ILogger _logger;

    public ReviewStore(string dataDir, ILogger logger)
    {
        _reviewsDir = System.IO.Path.Combine(dataDir, "reviews");
        System.IO.Directory.CreateDirectory(_reviewsDir);
        _logger = logger;
    }

    public ReviewComment AddComment(string commitSha, string filePath, int line, string author, string body, string? parentId)
    {
        var id = $"cmt_{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}{System.Random.Shared.Next(0x10000, 0xFFFFF):x}";
        var createdAt = System.DateTimeOffset.UtcNow;
        string rootId;

        if (parentId is not null)
        {
            var parent = GetComment(parentId);
            rootId = parent?.RootId ?? id;
        }
        else
        {
            rootId = id;
        }

        var hunkId = ComputeHunkId(filePath, line, body);
        var contextHash = ComputeContextHash(body);

        using var conn = OpenDb(commitSha);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO comments (id, root_id, parent_id, commit_sha, file_path, line, author, body, state, created_at, orphaned_at, hunk_id, context_hash)
            VALUES (@id, @root, @parent, @sha, @path, @line, @author, @body, 'open', @ts, NULL, @hunk, @ctx)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@root", rootId);
        cmd.Parameters.AddWithValue("@parent", (object?)parentId ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@sha", commitSha);
        cmd.Parameters.AddWithValue("@path", filePath);
        cmd.Parameters.AddWithValue("@line", line);
        cmd.Parameters.AddWithValue("@author", author);
        cmd.Parameters.AddWithValue("@body", body);
        cmd.Parameters.AddWithValue("@ts", createdAt.ToString("o"));
        cmd.Parameters.AddWithValue("@hunk", hunkId);
        cmd.Parameters.AddWithValue("@ctx", contextHash);
        cmd.ExecuteNonQuery();

        return new ReviewComment(id, rootId, parentId, commitSha, filePath, line, author, body, "open", createdAt, null, hunkId, contextHash);
    }

    public ReviewComment? GetComment(string commentId)
    {
        foreach (var dbPath in System.IO.Directory.EnumerateFiles(_reviewsDir, "review.db", System.IO.SearchOption.AllDirectories))
        {
            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, root_id, parent_id, commit_sha, file_path, line, author, body, state, created_at, orphaned_at, hunk_id, context_hash FROM comments WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", commentId);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
                return ReadComment(reader);
        }
        return null;
    }

    public System.Collections.Generic.IReadOnlyList<ReviewComment> ListComments(string commitSha, string? filePath = null, string? state = null)
    {
        var result = new System.Collections.Generic.List<ReviewComment>();
        using var conn = OpenDb(commitSha);
        using var cmd = conn.CreateCommand();
        var sql = "SELECT id, root_id, parent_id, commit_sha, file_path, line, author, body, state, created_at, orphaned_at, hunk_id, context_hash FROM comments WHERE 1=1";
        if (filePath is not null)
        {
            sql += " AND file_path = @path";
            cmd.Parameters.AddWithValue("@path", filePath);
        }
        if (state is not null)
        {
            sql += " AND state = @state";
            cmd.Parameters.AddWithValue("@state", state);
        }
        sql += " ORDER BY created_at";
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(ReadComment(reader));
        return result;
    }

    public void ResolveComment(string commentId, string actor)
    {
        TransitionState(commentId, "resolved", actor, null);
    }

    public void ReopenComment(string commentId, string actor)
    {
        TransitionState(commentId, "open", actor, null);
    }

    public void CloseComment(string commentId, string actor)
    {
        TransitionState(commentId, "closed", actor, null);
    }

    public void OrphanComment(string commentId)
    {
        var comment = GetComment(commentId);
        if (comment is null)
        {
            _logger.LogWarning("reviews: orphan — comment {id} not found", commentId);
            return;
        }

        var orphanedAt = System.DateTimeOffset.UtcNow;
        using var conn = OpenDb(comment.CommitSha);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE comments SET state = 'orphaned', orphaned_at = @ts WHERE id = @id";
        cmd.Parameters.AddWithValue("@ts", orphanedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@id", commentId);
        cmd.ExecuteNonQuery();

        AddAudit(conn, commentId, comment.State, "orphaned", "system", "anchor lost");
    }

    public void ReAnchorComment(string commentId, int newLine)
    {
        var comment = GetComment(commentId);
        if (comment is null)
        {
            _logger.LogWarning("reviews: re-anchor — comment {id} not found", commentId);
            return;
        }

        using var conn = OpenDb(comment.CommitSha);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE comments SET line = @line, state = 'open', orphaned_at = NULL WHERE id = @id";
        cmd.Parameters.AddWithValue("@line", newLine);
        cmd.Parameters.AddWithValue("@id", commentId);
        cmd.ExecuteNonQuery();

        AddAudit(conn, commentId, comment.State, "open", "system", $"re-anchored to line {newLine}");
    }

    public System.Collections.Generic.IReadOnlyList<ReviewAuditEntry> GetAuditTrail(string commentId)
    {
        var result = new System.Collections.Generic.List<ReviewAuditEntry>();
        var comment = GetComment(commentId);
        if (comment is null) return result;

        using var conn = OpenDb(comment.CommitSha);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, comment_id, from_state, to_state, actor, at, note FROM audit_trail WHERE comment_id = @id ORDER BY at";
        cmd.Parameters.AddWithValue("@id", commentId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new ReviewAuditEntry(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                System.DateTimeOffset.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
                reader.IsDBNull(6) ? null : reader.GetString(6)
            ));
        }
        return result;
    }

    public void AddTelemetry(string commitSha, string sessionId, string adapter, int filesTouched)
    {
        using var conn = OpenDb(commitSha);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO session_telemetry (session_id, commit_sha, adapter, files_touched)
            VALUES (@sid, @sha, @adapter, @files)
            ON CONFLICT(session_id) DO UPDATE SET files_touched = files_touched + @files
            """;
        cmd.Parameters.AddWithValue("@sid", sessionId);
        cmd.Parameters.AddWithValue("@sha", commitSha);
        cmd.Parameters.AddWithValue("@adapter", adapter);
        cmd.Parameters.AddWithValue("@files", filesTouched);
        cmd.ExecuteNonQuery();
    }

    public System.Collections.Generic.IReadOnlyList<SessionTelemetry> GetTelemetry(string commitSha)
    {
        var result = new System.Collections.Generic.List<SessionTelemetry>();
        using var conn = OpenDb(commitSha);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT session_id, adapter, files_touched FROM session_telemetry WHERE commit_sha = @sha ORDER BY session_id";
        cmd.Parameters.AddWithValue("@sha", commitSha);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new SessionTelemetry(reader.GetString(0), reader.GetString(1), reader.GetInt32(2)));
        }
        return result;
    }

    private void TransitionState(string commentId, string toState, string actor, string? note)
    {
        var comment = GetComment(commentId);
        if (comment is null)
        {
            _logger.LogWarning("reviews: transition — comment {id} not found", commentId);
            return;
        }

        var fromState = comment.State;
        using var conn = OpenDb(comment.CommitSha);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE comments SET state = @state WHERE id = @id";
        cmd.Parameters.AddWithValue("@state", toState);
        cmd.Parameters.AddWithValue("@id", commentId);
        cmd.ExecuteNonQuery();

        AddAudit(conn, commentId, fromState, toState, actor, note);
    }

    private static void AddAudit(SqliteConnection conn, string commentId, string fromState, string toState, string actor, string? note)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO audit_trail (id, comment_id, from_state, to_state, actor, at, note) VALUES (@id, @cid, @from, @to, @actor, @at, @note)";
        cmd.Parameters.AddWithValue("@id", System.Guid.NewGuid().ToString("N"));
        cmd.Parameters.AddWithValue("@cid", commentId);
        cmd.Parameters.AddWithValue("@from", fromState);
        cmd.Parameters.AddWithValue("@to", toState);
        cmd.Parameters.AddWithValue("@actor", actor);
        cmd.Parameters.AddWithValue("@at", System.DateTimeOffset.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@note", (object?)note ?? System.DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection OpenDb(string commitSha)
    {
        var dir = System.IO.Path.Combine(_reviewsDir, commitSha);
        System.IO.Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, "review.db");
        var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        EnsureSchema(conn);
        return conn;
    }

    private static void EnsureSchema(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS comments (
                id TEXT PRIMARY KEY,
                root_id TEXT NOT NULL,
                parent_id TEXT,
                commit_sha TEXT NOT NULL,
                file_path TEXT NOT NULL,
                line INTEGER NOT NULL,
                author TEXT NOT NULL,
                body TEXT NOT NULL,
                state TEXT NOT NULL DEFAULT 'open',
                created_at TEXT NOT NULL,
                orphaned_at TEXT,
                hunk_id TEXT,
                context_hash TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_comments_commit ON comments (commit_sha);
            CREATE INDEX IF NOT EXISTS idx_comments_file ON comments (file_path, line);
            CREATE INDEX IF NOT EXISTS idx_comments_state ON comments (state);

            CREATE TABLE IF NOT EXISTS audit_trail (
                id TEXT PRIMARY KEY,
                comment_id TEXT NOT NULL,
                from_state TEXT NOT NULL,
                to_state TEXT NOT NULL,
                actor TEXT NOT NULL,
                at TEXT NOT NULL,
                note TEXT,
                FOREIGN KEY (comment_id) REFERENCES comments (id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_audit_comment ON audit_trail (comment_id, at);

            CREATE TABLE IF NOT EXISTS attribution (
                id TEXT PRIMARY KEY,
                comment_id TEXT NOT NULL,
                authoring_tool_call TEXT,
                session_id TEXT,
                FOREIGN KEY (comment_id) REFERENCES comments (id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS session_telemetry (
                session_id TEXT PRIMARY KEY,
                commit_sha TEXT NOT NULL,
                adapter TEXT NOT NULL,
                files_touched INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_telemetry_commit ON session_telemetry (commit_sha);

            CREATE TABLE IF NOT EXISTS session_telemetry_files (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                file_path TEXT NOT NULL,
                FOREIGN KEY (session_id) REFERENCES session_telemetry (session_id) ON DELETE CASCADE
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static ReviewComment ReadComment(SqliteDataReader reader)
    {
        return new ReviewComment(
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt32(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            System.DateTimeOffset.Parse(reader.GetString(9), null, System.Globalization.DateTimeStyles.RoundtripKind),
            reader.IsDBNull(10) ? null : System.DateTimeOffset.Parse(reader.GetString(10), null, System.Globalization.DateTimeStyles.RoundtripKind),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetString(12)
        );
    }

    private static string ComputeHunkId(string filePath, int line, string body)
    {
        var input = $"{filePath}:{line}:{body}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return System.Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeContextHash(string body)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(body);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return System.Convert.ToHexString(hash).ToLowerInvariant();
    }
}
