namespace Cove.Persistence;

public static class ProbeSchema
{
    public const string Ddl = """
        CREATE TABLE IF NOT EXISTS schema_meta (
          key   TEXT PRIMARY KEY,
          value TEXT NOT NULL
        ) WITHOUT ROWID;

        INSERT OR REPLACE INTO schema_meta(key, value) VALUES ('probe_schema_version', '1');

        CREATE TABLE IF NOT EXISTS probe (
          id         TEXT PRIMARY KEY,
          created_at INTEGER NOT NULL,
          title      TEXT NOT NULL,
          body       TEXT NOT NULL
        );

        CREATE VIRTUAL TABLE IF NOT EXISTS probe_fts USING fts5(
          title,
          body,
          content='probe',
          content_rowid='rowid',
          tokenize='porter unicode61 remove_diacritics 1',
          prefix='2 3'
        );

        CREATE TRIGGER IF NOT EXISTS probe_fts_ai AFTER INSERT ON probe BEGIN
          INSERT INTO probe_fts(rowid, title, body) VALUES (new.rowid, new.title, new.body);
        END;

        CREATE TRIGGER IF NOT EXISTS probe_fts_ad AFTER DELETE ON probe BEGIN
          INSERT INTO probe_fts(probe_fts, rowid, title, body) VALUES ('delete', old.rowid, old.title, old.body);
        END;

        CREATE TRIGGER IF NOT EXISTS probe_fts_au AFTER UPDATE ON probe BEGIN
          INSERT INTO probe_fts(probe_fts, rowid, title, body) VALUES ('delete', old.rowid, old.title, old.body);
          INSERT INTO probe_fts(rowid, title, body) VALUES (new.rowid, new.title, new.body);
        END;
        """;
}
