using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed record LibraryEntry(string Id, string BayId, string NookId, string NookType, string? Title, string? StateJson, string? Scrollback, string Kind, System.DateTimeOffset CapturedAt);

public sealed class LibraryStore
{
    private readonly SqliteConnectionFactory _database;
    private readonly string _entriesDir;
    private readonly ILogger _logger;

    private static readonly System.Text.RegularExpressions.Regex SecretPattern = new(
        @"(password|passwd|secret|token|api[_-]?key|bearer|authorization|credential)",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex ValueAfterSecretPattern = new(
        @"(\[[A-Z]+\]\s*[""']?\s*:?\s*)([""'])([^""']{3,})",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex KnownSecretFormatPattern = new(
        @"(sk-[a-zA-Z0-9]{20,}|AKIA[0-9A-Z]{16}|gh[pousr]_[a-zA-Z0-9]{36}|xox[baprs]-[a-zA-Z0-9-]+|eyJ[a-zA-Z0-9_-]+\.eyJ[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+|-----BEGIN [A-Z ]*PRIVATE KEY-----)",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    public LibraryStore(
        string dataDir,
        ILogger logger,
        SqliteConnectionFactory? database = null)
    {
        var databasePath = System.IO.Path.Combine(dataDir, "library", "catalog.db");
        _entriesDir = System.IO.Path.Combine(dataDir, "library", "entries");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(databasePath)!);
        System.IO.Directory.CreateDirectory(_entriesDir);
        _logger = logger;
        _database = database ?? new SqliteConnectionFactory(databasePath, logger);
    }

    public void EnsureSchema()
    {
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS catalog (
                id TEXT PRIMARY KEY,
                bay_id TEXT NOT NULL,
                nook_id TEXT NOT NULL,
                nook_type TEXT NOT NULL,
                title TEXT,
                state_json TEXT,
                scrollback TEXT,
                kind TEXT NOT NULL,
                captured_at TEXT NOT NULL,
                pinned INTEGER NOT NULL DEFAULT 0,
                archived INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_catalog_bay ON catalog (bay_id, captured_at DESC);
            CREATE INDEX IF NOT EXISTS idx_catalog_kind ON catalog (kind);
            """;
        cmd.ExecuteNonQuery();
    }

    public LibraryEntry SaveEntry(string bayId, string nookId, string nookType, string title, string? stateJson, string? scrollback, string kind)
    {
        var id = System.Guid.NewGuid().ToString("N");
        var capturedAt = System.DateTimeOffset.UtcNow;

        var redactedState = RedactSecrets(stateJson);
        var redactedScrollback = RedactSecrets(scrollback);

        var entry = new LibraryEntry(id, bayId, nookId, nookType, title, redactedState, redactedScrollback, kind, capturedAt);

        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO catalog (id, bay_id, nook_id, nook_type, title, state_json, scrollback, kind, captured_at)
            VALUES (@id, @ws, @pid, @ptype, @title, @state, @sb, @kind, @ts);
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@ws", bayId);
        cmd.Parameters.AddWithValue("@pid", nookId);
        cmd.Parameters.AddWithValue("@ptype", nookType);
        cmd.Parameters.AddWithValue("@title", (object?)title ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@state", (object?)redactedState ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@sb", (object?)redactedScrollback ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("@kind", kind);
        cmd.Parameters.AddWithValue("@ts", capturedAt.ToString("o"));
        cmd.ExecuteNonQuery();

        var entryPath = System.IO.Path.Combine(_entriesDir, id + ".json");
        System.IO.File.WriteAllText(entryPath, JsonSerializer.Serialize(entry, LibraryJsonContext.Default.LibraryEntry));

        _logger.LogWarning("library: saved entry {id} ({kind}) for nook {pid} in {ws}", id, kind, nookId, bayId);
        return entry;
    }

    public System.Collections.Generic.IReadOnlyList<LibraryEntry> ListByBay(string bayId, string? kind = null)
    {
        var result = new System.Collections.Generic.List<LibraryEntry>();
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        if (kind is null)
        {
            cmd.CommandText = "SELECT id, bay_id, nook_id, nook_type, title, state_json, scrollback, kind, captured_at FROM catalog WHERE bay_id = @ws AND archived = 0 ORDER BY pinned DESC, captured_at DESC";
            cmd.Parameters.AddWithValue("@ws", bayId);
        }
        else
        {
            cmd.CommandText = "SELECT id, bay_id, nook_id, nook_type, title, state_json, scrollback, kind, captured_at FROM catalog WHERE bay_id = @ws AND kind = @kind AND archived = 0 ORDER BY pinned DESC, captured_at DESC";
            cmd.Parameters.AddWithValue("@ws", bayId);
            cmd.Parameters.AddWithValue("@kind", kind);
        }
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(ReadEntry(reader));
        return result;
    }

    public void Pin(string id)
    {
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE catalog SET pinned = 1 WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void Archive(string id)
    {
        using var conn = _database.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE catalog SET archived = 1 WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    private static LibraryEntry ReadEntry(SqliteDataReader reader)
    {
        return new LibraryEntry(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetString(7),
            System.DateTimeOffset.Parse(reader.GetString(8), null, System.Globalization.DateTimeStyles.RoundtripKind)
        );
    }
    public string? RedactSecrets(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var redacted = SecretPattern.Replace(input, "[REDACTED]");
        redacted = ValueAfterSecretPattern.Replace(redacted, "[REDACTED]");
        redacted = KnownSecretFormatPattern.Replace(redacted, "[REDACTED]");
        return redacted;
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LibraryEntry))]
[JsonSerializable(typeof(System.Collections.Generic.List<LibraryEntry>))]
public sealed partial class LibraryJsonContext : JsonSerializerContext { }
