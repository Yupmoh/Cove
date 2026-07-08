using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed class KnowledgePersistenceKernel
{
    private readonly string _dataDir;
    private readonly ILogger _logger;

    public KnowledgePersistenceKernel(string dataDir, ILogger logger)
    {
        _dataDir = dataDir;
        _logger = logger;
    }

    public void EnsureAllSchemas()
    {
        EnsureTimelineDb();
        EnsureMemoryDb();
        EnsureFtsIndexDb();
        EnsureNotesIndexDb();
        VerifyTokenizers();
    }

    private void EnsureTimelineDb()
    {
        var path = System.IO.Path.Combine(_dataDir, "timeline.db");
        using var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
            cmd.ExecuteNonQuery();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS timeline (
                    id TEXT PRIMARY KEY,
                    workspace_id TEXT NOT NULL,
                    kind TEXT NOT NULL,
                    scope TEXT NOT NULL,
                    title TEXT,
                    body TEXT,
                    metadata_json TEXT,
                    tags_json TEXT,
                    pane_id TEXT,
                    created_at TEXT NOT NULL,
                    backfill_key TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_timeline_workspace ON timeline (workspace_id, created_at);
                CREATE INDEX IF NOT EXISTS idx_timeline_kind ON timeline (kind);
                CREATE INDEX IF NOT EXISTS idx_timeline_scope ON timeline (scope);
                CREATE UNIQUE INDEX IF NOT EXISTS idx_timeline_backfill ON timeline (backfill_key) WHERE backfill_key IS NOT NULL;
                CREATE VIRTUAL TABLE IF NOT EXISTS timeline_fts USING fts5(title, body, content='timeline', content_rowid='rowid');
                CREATE TRIGGER IF NOT EXISTS timeline_ai AFTER INSERT ON timeline BEGIN
                    INSERT INTO timeline_fts(rowid, title, body) VALUES (new.rowid, new.title, new.body);
                END;
                CREATE TRIGGER IF NOT EXISTS timeline_ad AFTER DELETE ON timeline BEGIN
                    INSERT INTO timeline_fts(timeline_fts, rowid, title, body) VALUES('delete', old.rowid, old.title, old.body);
                END;
                CREATE TRIGGER IF NOT EXISTS timeline_au AFTER UPDATE ON timeline BEGIN
                    INSERT INTO timeline_fts(timeline_fts, rowid, title, body) VALUES('delete', old.rowid, old.title, old.body);
                    INSERT INTO timeline_fts(rowid, title, body) VALUES (new.rowid, new.title, new.body);
                END;
                """;
            cmd.ExecuteNonQuery();
        }
        _logger.LogWarning("knowledge: timeline.db schema ensured at {path}", path);
    }

    private void EnsureMemoryDb()
    {
        var path = System.IO.Path.Combine(_dataDir, "memory", "memory.db");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        using var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS facts (
                id TEXT PRIMARY KEY,
                workspace_id TEXT NOT NULL,
                kind TEXT NOT NULL,
                content TEXT NOT NULL,
                confidence REAL NOT NULL DEFAULT 0.5,
                access_count INTEGER NOT NULL DEFAULT 0,
                audience TEXT,
                locus TEXT,
                file_path TEXT,
                superseded_by TEXT,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS episodes (
                id TEXT PRIMARY KEY,
                workspace_id TEXT NOT NULL,
                session_id TEXT,
                summary_l0 TEXT,
                summary_l1 TEXT,
                summary_l2 TEXT,
                files_touched_json TEXT,
                created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS blackboard (
                id TEXT PRIMARY KEY,
                workspace_id TEXT NOT NULL,
                kind TEXT NOT NULL,
                audience TEXT NOT NULL,
                content TEXT NOT NULL,
                ref_id TEXT,
                ttl_at TEXT,
                created_at TEXT NOT NULL
            );
            CREATE VIRTUAL TABLE IF NOT EXISTS facts_fts USING fts5(content, content='facts', content_rowid='rowid', tokenize='porter unicode61 remove_diacritics 1');
            CREATE VIRTUAL TABLE IF NOT EXISTS episodes_fts USING fts5(summary_l0, content='episodes', content_rowid='rowid', tokenize='porter unicode61 remove_diacritics 1');
            """;
        cmd.ExecuteNonQuery();
        _logger.LogWarning("knowledge: memory.db schema ensured at {path}", path);
    }

    private void EnsureFtsIndexDb()
    {
        var path = System.IO.Path.Combine(_dataDir, "fts", "index.db");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        using var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS sessions (
                id TEXT PRIMARY KEY,
                workspace_id TEXT NOT NULL,
                adapter TEXT NOT NULL,
                started_at TEXT NOT NULL,
                ended_at TEXT,
                extractor_version TEXT
            );
            CREATE TABLE IF NOT EXISTS agent_edits (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                file_path TEXT NOT NULL,
                tool TEXT,
                op TEXT,
                occurred_at TEXT NOT NULL,
                edit_summary TEXT,
                FOREIGN KEY (session_id) REFERENCES sessions (id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_edits_file ON agent_edits (file_path, occurred_at);
            CREATE INDEX IF NOT EXISTS idx_edits_session ON agent_edits (session_id);
            CREATE VIRTUAL TABLE IF NOT EXISTS sessions_fts USING fts5(adapter, content='sessions', content_rowid='rowid');
            CREATE VIRTUAL TABLE IF NOT EXISTS sessions_fts_trigram USING fts5(adapter, content='sessions', content_rowid='rowid', tokenize='trigram');
            """;
        cmd.ExecuteNonQuery();
        _logger.LogWarning("knowledge: fts/index.db schema ensured at {path}", path);
    }

    private void EnsureNotesIndexDb()
    {
        var path = System.IO.Path.Combine(_dataDir, "notes", "index.db");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        using var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS notes_index (
                note_id TEXT PRIMARY KEY,
                workspace_id TEXT NOT NULL,
                title TEXT,
                body TEXT,
                type TEXT NOT NULL,
                folder TEXT,
                tags_json TEXT,
                source TEXT,
                updated_at TEXT NOT NULL
            );
            CREATE VIRTUAL TABLE IF NOT EXISTS notes_fts USING fts5(title, body, content='notes_index', content_rowid='rowid', tokenize='porter unicode61 remove_diacritics 1');
            """;
        cmd.ExecuteNonQuery();
        _logger.LogWarning("knowledge: notes/index.db schema ensured at {path}", path);
    }

    private void VerifyTokenizers()
    {
        using var conn = new SqliteConnection($"Data Source={System.IO.Path.Combine(_dataDir, "timeline.db")}");
        conn.Open();
        foreach (var tokenizer in new[] { "porter", "trigram", "unicode61" })
        {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"CREATE VIRTUAL TABLE IF NOT EXISTS _tokenizer_check USING fts5(x, tokenize='{tokenizer}'); DROP TABLE IF EXISTS _tokenizer_check;";
                cmd.ExecuteNonQuery();
            }
            catch (SqliteException ex)
            {
                _logger.LogError("knowledge: FTS5 tokenizer '{tokenizer}' not available — startup self-check FAILED: {error}", tokenizer, ex.Message);
                throw new System.InvalidOperationException($"FTS5 tokenizer '{tokenizer}' is not available. The bundled SQLite must include FTS5 with {tokenizer} support.");
            }
        }
        _logger.LogWarning("knowledge: FTS5 tokenizers verified (porter, trigram, unicode61)");
    }
}
