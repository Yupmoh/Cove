using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Browser;

public sealed record BrowserHistoryEntry(string Url, string Title, int VisitCount, System.DateTimeOffset LastVisited);

public sealed record BrowserAnnotation(string Id, string UrlKey, string Kind, string AnchorJson, string Text, bool Resolved, string Source, System.DateTimeOffset CreatedAt);


public sealed class BrowserStore
{
    private readonly string _dbPath;
    private readonly ILogger _logger;

    public BrowserStore(string dataDir, ILogger logger)
    {
        _dbPath = System.IO.Path.Combine(dataDir, "browser", "browser.db");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_dbPath)!);
        _logger = logger;
        EnsureSchema();
    }

    public void RecordVisit(string url, string title)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO history (url, title, visit_count, last_visited)
            VALUES (@url, @title, 1, @ts)
            ON CONFLICT(url) DO UPDATE SET
                title = @title,
                visit_count = visit_count + 1,
                last_visited = @ts
            """;
        cmd.Parameters.AddWithValue("@url", url);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@ts", System.DateTimeOffset.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public System.Collections.Generic.IReadOnlyList<BrowserHistoryEntry> SearchHistory(string query, int limit = 20)
    {
        var result = new System.Collections.Generic.List<BrowserHistoryEntry>();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT url, title, visit_count, last_visited FROM history
            WHERE url LIKE @q OR title LIKE @q
            ORDER BY visit_count DESC, last_visited DESC
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@q", $"%{query}%");
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new BrowserHistoryEntry(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                System.DateTimeOffset.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind)
            ));
        }
        return result;
    }

    public System.Collections.Generic.IReadOnlyList<BrowserHistoryEntry> GetTopVisited(int limit = 10)
    {
        var result = new System.Collections.Generic.List<BrowserHistoryEntry>();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT url, title, visit_count, last_visited FROM history ORDER BY visit_count DESC, last_visited DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new BrowserHistoryEntry(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                System.DateTimeOffset.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind)
            ));
        }
        return result;
    }

    public BrowserAnnotation AddAnnotation(string url, string kind, string anchorJson, string text, string source)
    {
        var id = System.Guid.NewGuid().ToString("N");
        var urlKey = NormalizeUrlKey(url);
        var createdAt = System.DateTimeOffset.UtcNow;

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO annotations (id, url_key, kind, anchor_json, text, resolved, source, created_at)
            VALUES (@id, @urlKey, @kind, @anchor, @text, 0, @source, @ts)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@urlKey", urlKey);
        cmd.Parameters.AddWithValue("@kind", kind);
        cmd.Parameters.AddWithValue("@anchor", anchorJson);
        cmd.Parameters.AddWithValue("@text", text);
        cmd.Parameters.AddWithValue("@source", source);
        cmd.Parameters.AddWithValue("@ts", createdAt.ToString("o"));
        cmd.ExecuteNonQuery();

        return new BrowserAnnotation(id, urlKey, kind, anchorJson, text, false, source, createdAt);
    }

    public System.Collections.Generic.IReadOnlyList<BrowserAnnotation> ListAnnotations(string? urlKey = null, bool? unresolved = null)
    {
        var result = new System.Collections.Generic.List<BrowserAnnotation>();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        var sql = "SELECT id, url_key, kind, anchor_json, text, resolved, source, created_at FROM annotations WHERE 1=1";
        if (urlKey is not null)
        {
            sql += " AND url_key = @urlKey";
            cmd.Parameters.AddWithValue("@urlKey", urlKey);
        }
        if (unresolved == true)
        {
            sql += " AND resolved = 0";
        }
        sql += " ORDER BY created_at DESC";
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new BrowserAnnotation(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5) == 1,
                reader.GetString(6),
                System.DateTimeOffset.Parse(reader.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind)
            ));
        }
        return result;
    }

    public bool ResolveAnnotation(string id)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE annotations SET resolved = 1 WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool DeleteAnnotation(string id)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM annotations WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    public string ExportHistoryToJsonFile()
    {
        var exportPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_dbPath)!, "browser-history.json");
        var entries = new System.Collections.Generic.List<string>();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT url, title, visit_count, last_visited FROM history ORDER BY last_visited DESC";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var url = EscapeJsonString(reader.GetString(0));
            var title = reader.IsDBNull(1) ? "" : EscapeJsonString(reader.GetString(1));
            var visitCount = reader.GetInt32(2);
            var lastVisited = EscapeJsonString(reader.GetString(3));
            entries.Add($"    {{\"url\": \"{url}\", \"title\": \"{title}\", \"visit_count\": {visitCount}, \"last_visited\": \"{lastVisited}\"}}");
        }
        var json = entries.Count == 0 ? "[]" : "[\n" + string.Join(",\n", entries) + "\n]";
        var tmp = exportPath + ".tmp";
        System.IO.File.WriteAllText(tmp, json);
        System.IO.File.Move(tmp, exportPath, true);
        return exportPath;
    }

    private static string EscapeJsonString(string s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append($"\\u{(int)c:X4}");
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
    public static string NormalizeUrlKey(string url)
    {
        if (System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath == "/" ? "" : uri.AbsolutePath;
            return $"{uri.Scheme}://{uri.Host}{path}{uri.Query}";
        }
        return url;
    }
    private void EnsureSchema()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS history (
                url TEXT PRIMARY KEY,
                title TEXT,
                visit_count INTEGER NOT NULL DEFAULT 0,
                last_visited TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_history_title ON history (title);
            CREATE TABLE IF NOT EXISTS annotations (
                id TEXT PRIMARY KEY,
                url_key TEXT NOT NULL,
                kind TEXT NOT NULL,
                anchor_json TEXT NOT NULL,
                text TEXT NOT NULL,
                resolved INTEGER NOT NULL DEFAULT 0,
                source TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_annotations_url ON annotations (url_key, resolved);
            """;
        cmd.ExecuteNonQuery();
    }
}
