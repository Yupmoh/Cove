using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Captures;

public sealed record Capture(
    string Id,
    int Number,
    string BundleDir,
    string BayId,
    string Region,
    bool Audio,
    bool Mic,
    bool Cursor,
    System.DateTimeOffset CreatedAt,
    System.TimeSpan Duration,
    string Status);

public sealed class CaptureStore
{
    private readonly string _capturesDir;
    private readonly string _dbPath;
    private readonly ILogger _logger;
    private int _nextNumber = 1;

    public CaptureStore(string dataDir, ILogger logger)
    {
        _capturesDir = System.IO.Path.Combine(dataDir, "captures");
        _dbPath = System.IO.Path.Combine(dataDir, "captures.db");
        System.IO.Directory.CreateDirectory(_capturesDir);
        _logger = logger;
        EnsureSchema();
        _nextNumber = GetMaxNumber() + 1;
    }

    public Capture StartCapture(string bayId, string region, bool audio, bool mic, bool cursor)
    {
        var number = System.Threading.Interlocked.Increment(ref _nextNumber) - 1;
        var id = System.Guid.NewGuid().ToString("N");
        var createdAt = System.DateTimeOffset.UtcNow;
        var bundleDir = System.IO.Path.Combine(_capturesDir, $"CAP-{number}");
        System.IO.Directory.CreateDirectory(bundleDir);

        var capture = new Capture(id, number, bundleDir, bayId, region, audio, mic, cursor, createdAt, System.TimeSpan.Zero, "recording");

        WriteMetaJson(capture);

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO captures (id, number, bundle_dir, bay_id, region, audio, mic, cursor, created_at, duration_ms, status)
            VALUES (@id, @num, @dir, @ws, @region, @audio, @mic, @cursor, @ts, 0, 'recording')
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@num", number);
        cmd.Parameters.AddWithValue("@dir", bundleDir);
        cmd.Parameters.AddWithValue("@ws", bayId);
        cmd.Parameters.AddWithValue("@region", region);
        cmd.Parameters.AddWithValue("@audio", audio ? 1 : 0);
        cmd.Parameters.AddWithValue("@mic", mic ? 1 : 0);
        cmd.Parameters.AddWithValue("@cursor", cursor ? 1 : 0);
        cmd.Parameters.AddWithValue("@ts", createdAt.ToString("o"));
        cmd.ExecuteNonQuery();

        return capture;
    }

    public Capture? StopCapture(string id)
    {
        var cap = GetCapture(id);
        if (cap is null) return null;

        var stoppedAt = System.DateTimeOffset.UtcNow;
        var duration = stoppedAt - cap.CreatedAt;
        var updated = cap with { Duration = duration, Status = "stopped" };

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE captures SET duration_ms = @dur, status = 'stopped' WHERE id = @id";
        cmd.Parameters.AddWithValue("@dur", (long)duration.TotalMilliseconds);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();

        return updated;
    }

    public Capture? GetCapture(string id)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, number, bundle_dir, bay_id, region, audio, mic, cursor, created_at, duration_ms, status FROM captures WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return ReadCapture(reader);
    }

    public System.Collections.Generic.IReadOnlyList<Capture> ListCaptures()
    {
        var result = new System.Collections.Generic.List<Capture>();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, number, bundle_dir, bay_id, region, audio, mic, cursor, created_at, duration_ms, status FROM captures ORDER BY number ASC";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(ReadCapture(reader));
        return result;
    }

    public bool DeleteCapture(string id)
    {
        var cap = GetCapture(id);
        if (cap is null) return false;

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var delCmd = conn.CreateCommand();
        delCmd.CommandText = "DELETE FROM captures WHERE id = @id; DELETE FROM capture_attachments WHERE capture_id = @id";
        delCmd.Parameters.AddWithValue("@id", id);
        delCmd.ExecuteNonQuery();

        try
        {
            if (System.IO.Directory.Exists(cap.BundleDir))
                System.IO.Directory.Delete(cap.BundleDir, recursive: true);
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "capture: failed to delete bundle dir {dir}", cap.BundleDir);
        }

        return true;
    }

    public void AttachToTask(string captureId, string taskId)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO capture_attachments (capture_id, task_id) VALUES (@cap, @task)";
        cmd.Parameters.AddWithValue("@cap", captureId);
        cmd.Parameters.AddWithValue("@task", taskId);
        cmd.ExecuteNonQuery();
    }

    public System.Collections.Generic.IReadOnlyList<string> GetTaskAttachments(string taskId)
    {
        var result = new System.Collections.Generic.List<string>();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT capture_id FROM capture_attachments WHERE task_id = @task ORDER BY rowid";
        cmd.Parameters.AddWithValue("@task", taskId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(reader.GetString(0));
        return result;
    }

    public void FlagCapture(string id, string label)
    {
        var cap = GetCapture(id);
        if (cap is null) return;

        var chaptersPath = System.IO.Path.Combine(cap.BundleDir, "chapters.json");
        var chapters = new System.Collections.Generic.List<string>();
        if (System.IO.File.Exists(chaptersPath))
        {
            var existing = System.IO.File.ReadAllText(chaptersPath);
            if (existing.Trim() != "[]" && !string.IsNullOrEmpty(existing))
                chapters.Add(existing.TrimStart('[').TrimEnd(']'));
        }

        var offsetMs = (long)(System.DateTimeOffset.UtcNow - cap.CreatedAt).TotalMilliseconds;
        chapters.Add($$"""{"offsetMs": {{offsetMs}}, "label": "{{EscapeJson(label)}}"}""");

        var json = "[\n  " + string.Join(",\n  ", chapters) + "\n]";
        System.IO.File.WriteAllText(chaptersPath, json);
    }

    private static Capture ReadCapture(SqliteDataReader reader)
    {
        return new Capture(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt32(5) == 1,
            reader.GetInt32(6) == 1,
            reader.GetInt32(7) == 1,
            System.DateTimeOffset.Parse(reader.GetString(8), null, System.Globalization.DateTimeStyles.RoundtripKind),
            System.TimeSpan.FromMilliseconds(reader.GetInt64(9)),
            reader.GetString(10)
        );
    }

    private void WriteMetaJson(Capture cap)
    {
        var metaPath = System.IO.Path.Combine(cap.BundleDir, "meta.json");
        var json = $$"""
        {
          "id": "{{cap.Id}}",
          "number": {{cap.Number}},
          "region": "{{cap.Region}}",
          "audio": {{cap.Audio.ToString().ToLowerInvariant()}},
          "mic": {{cap.Mic.ToString().ToLowerInvariant()}},
          "cursor": {{cap.Cursor.ToString().ToLowerInvariant()}},
          "duration": 0,
          "createdAt": "{{cap.CreatedAt:o}}",
          "bayId": "{{cap.BayId}}",
          "taskAttachments": []
        }
        """;
        System.IO.File.WriteAllText(metaPath, json);
    }

    private int GetMaxNumber()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(number), 0) FROM captures";
        var result = cmd.ExecuteScalar();
        return result is int i ? i : 0;
    }

    private static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private void EnsureSchema()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS captures (
                id TEXT PRIMARY KEY,
                number INTEGER NOT NULL,
                bundle_dir TEXT NOT NULL,
                bay_id TEXT NOT NULL,
                region TEXT NOT NULL,
                audio INTEGER NOT NULL DEFAULT 0,
                mic INTEGER NOT NULL DEFAULT 0,
                cursor INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                duration_ms INTEGER NOT NULL DEFAULT 0,
                status TEXT NOT NULL DEFAULT 'recording'
            );
            CREATE INDEX IF NOT EXISTS idx_captures_bay ON captures (bay_id);
            CREATE TABLE IF NOT EXISTS capture_attachments (
                capture_id TEXT NOT NULL,
                task_id TEXT NOT NULL,
                PRIMARY KEY (capture_id, task_id)
            );
            CREATE INDEX IF NOT EXISTS idx_attachments_task ON capture_attachments (task_id);
            """;
        cmd.ExecuteNonQuery();
    }
}
