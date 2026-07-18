using Cove.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed class KnowledgePersistenceKernel
{
    private readonly string _timelinePath;
    private readonly string _memoryPath;
    private readonly string _ftsIndexPath;
    private readonly string _notesIndexPath;
    private readonly SqliteConnectionFactory _timelineDatabase;
    private readonly SqliteConnectionFactory _memoryDatabase;
    private readonly SqliteConnectionFactory _ftsIndexDatabase;
    private readonly SqliteConnectionFactory _notesIndexDatabase;
    private readonly ILogger _logger;

    public KnowledgePersistenceKernel(string dataDir, ILogger logger)
    {
        _timelinePath = System.IO.Path.Combine(dataDir, "timeline.db");
        _memoryPath = System.IO.Path.Combine(dataDir, "memory", "memory.db");
        _ftsIndexPath = System.IO.Path.Combine(dataDir, "fts", "index.db");
        _notesIndexPath = System.IO.Path.Combine(dataDir, "notes", "index.db");
        _logger = logger;
        _timelineDatabase = new SqliteConnectionFactory(_timelinePath, logger);
        _memoryDatabase = new SqliteConnectionFactory(_memoryPath, logger);
        _ftsIndexDatabase = new SqliteConnectionFactory(_ftsIndexPath, logger);
        _notesIndexDatabase = new SqliteConnectionFactory(_notesIndexPath, logger);
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
        using var conn = _timelineDatabase.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS timeline (
                id TEXT PRIMARY KEY,
                bay_id TEXT NOT NULL,
                kind TEXT NOT NULL,
                scope TEXT,
                title TEXT,
                body TEXT,
                metadata_json TEXT,
                tags_json TEXT,
                nook_id TEXT,
                created_at TEXT NOT NULL,
                backfill_key TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_timeline_bay ON timeline (bay_id, created_at);
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
        _logger.LogWarning("knowledge: timeline.db schema ensured at {path}", _timelinePath);
    }

    private void EnsureMemoryDb()
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_memoryPath)!);
        using var conn = _memoryDatabase.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS facts (
                id TEXT PRIMARY KEY,
                bay_id TEXT NOT NULL,
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
                bay_id TEXT NOT NULL,
                session_id TEXT,
                summary_l0 TEXT,
                summary_l1 TEXT,
                summary_l2 TEXT,
                files_touched_json TEXT,
                created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS blackboard (
                id TEXT PRIMARY KEY,
                bay_id TEXT NOT NULL,
                kind TEXT NOT NULL,
                audience TEXT NOT NULL,
                content TEXT NOT NULL,
                ref_id TEXT,
                ttl_at TEXT,
                created_at TEXT NOT NULL
            );
            CREATE VIRTUAL TABLE IF NOT EXISTS facts_fts USING fts5(content, content='facts', content_rowid='rowid', tokenize='porter unicode61 remove_diacritics 1');
            CREATE TRIGGER IF NOT EXISTS facts_ai AFTER INSERT ON facts BEGIN
                INSERT INTO facts_fts(rowid, content) VALUES (new.rowid, new.content);
            END;
            CREATE TRIGGER IF NOT EXISTS facts_ad AFTER DELETE ON facts BEGIN
                INSERT INTO facts_fts(facts_fts, rowid, content) VALUES('delete', old.rowid, old.content);
            END;
            CREATE TRIGGER IF NOT EXISTS facts_au AFTER UPDATE ON facts BEGIN
                INSERT INTO facts_fts(facts_fts, rowid, content) VALUES('delete', old.rowid, old.content);
                INSERT INTO facts_fts(rowid, content) VALUES (new.rowid, new.content);
            END;
            CREATE VIRTUAL TABLE IF NOT EXISTS episodes_fts USING fts5(summary_l0, content='episodes', content_rowid='rowid', tokenize='porter unicode61 remove_diacritics 1');
            """;
        cmd.ExecuteNonQuery();
        _logger.LogWarning("knowledge: memory.db schema ensured at {path}", _memoryPath);
    }

    private void EnsureFtsIndexDb()
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_ftsIndexPath)!);
        using var conn = _ftsIndexDatabase.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS sessions (
                id TEXT PRIMARY KEY,
                bay_id TEXT NOT NULL,
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
        _logger.LogWarning("knowledge: fts/index.db schema ensured at {path}", _ftsIndexPath);
    }

    private void EnsureNotesIndexDb()
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_notesIndexPath)!);
        using var conn = _notesIndexDatabase.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS notes_index (
                note_id TEXT PRIMARY KEY,
                bay_id TEXT NOT NULL,
                title TEXT,
                body TEXT,
                type TEXT NOT NULL,
                folder TEXT,
                tags_json TEXT,
                source TEXT,
                updated_at TEXT NOT NULL
            );
            CREATE VIRTUAL TABLE IF NOT EXISTS notes_fts USING fts5(title, body, content='notes_index', content_rowid='rowid', tokenize='porter unicode61 remove_diacritics 1');
            CREATE TRIGGER IF NOT EXISTS notes_ai AFTER INSERT ON notes_index BEGIN
                INSERT INTO notes_fts(rowid, title, body) VALUES (new.rowid, new.title, new.body);
            END;
            CREATE TRIGGER IF NOT EXISTS notes_ad AFTER DELETE ON notes_index BEGIN
                INSERT INTO notes_fts(notes_fts, rowid, title, body) VALUES('delete', old.rowid, old.title, old.body);
            END;
            CREATE TRIGGER IF NOT EXISTS notes_au AFTER UPDATE ON notes_index BEGIN
                INSERT INTO notes_fts(notes_fts, rowid, title, body) VALUES('delete', old.rowid, old.title, old.body);
                INSERT INTO notes_fts(rowid, title, body) VALUES (new.rowid, new.title, new.body);
            END;
            """;
        cmd.ExecuteNonQuery();
        _logger.LogWarning("knowledge: notes/index.db schema ensured at {path}", _notesIndexPath);
    }

    private void VerifyTokenizers()
    {
        using var conn = _timelineDatabase.Open();
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
