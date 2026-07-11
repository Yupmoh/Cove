using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Telemetry;

public sealed record TelemetryEvent(string Id, string Category, string Name, System.DateTimeOffset At, string PayloadJson);

public sealed class TelemetryService
{
    private readonly string _dbPath;
    private readonly string _deviceIdPath;
    private readonly ILogger _logger;
    private bool _enabled;
    private string? _deviceId;
    private readonly object _lock = new();

    public TelemetryService(string dataDir, ILogger logger)
    {
        _dbPath = System.IO.Path.Combine(dataDir, "telemetry_queue.db");
        _deviceIdPath = System.IO.Path.Combine(dataDir, "device-id");
        _logger = logger;
    }

    public bool IsEnabled
    {
        get { lock (_lock) return _enabled; }
        set
        {
            lock (_lock)
            {
                _enabled = value;
                if (_enabled && !System.IO.File.Exists(_dbPath))
                    EnsureSchema();
                if (_enabled && _deviceId is null && System.IO.File.Exists(_deviceIdPath))
                    _deviceId = System.IO.File.ReadAllText(_deviceIdPath);
                if (_enabled && _deviceId is null)
                {
                    _deviceId = System.Guid.NewGuid().ToString("N");
                    System.IO.File.WriteAllText(_deviceIdPath, _deviceId);
                    _logger.LogWarning("telemetry: device-id created (opt-in)");
                }
            }
        }
    }

    public string? DeviceId
    {
        get { lock (_lock) return _deviceId; }
    }

    public bool DatabaseExists => System.IO.File.Exists(_dbPath);
    public bool DeviceIdExists => System.IO.File.Exists(_deviceIdPath);

    public void Record(string category, string name, System.Collections.Generic.IReadOnlyDictionary<string, object> payload)
    {
        lock (_lock)
        {
            if (!_enabled)
            {
                _logger.LogWarning("telemetry: record skipped (not enabled)");
                return;
            }

            foreach (var kv in payload)
            {
                if (!IsAllowedField(kv.Key))
                    throw new System.InvalidOperationException($"telemetry schema violation: key '{kv.Key}' is not in the closed field allowlist");
                if (!IsAllowedPrimitive(kv.Value))
                    throw new System.InvalidOperationException($"telemetry schema violation: key '{kv.Key}' has non-primitive type {kv.Value?.GetType().Name}");
                if (kv.Value is string s && s.Length > 256)
                    throw new System.InvalidOperationException($"telemetry schema violation: key '{kv.Key}' string exceeds 256 chars (possible content leak)");
            }

            EnsureSchema();
            var id = System.Guid.NewGuid().ToString("N");
            var at = System.DateTimeOffset.UtcNow;
            var payloadJson = SerializePayload(payload);

            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO telemetry_queue (id, category, name, at, payload) VALUES (@id, @cat, @name, @at, @payload)";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@cat", category);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@at", at.ToString("o"));
            cmd.Parameters.AddWithValue("@payload", payloadJson);
            cmd.ExecuteNonQuery();
        }
    }

    public System.Collections.Generic.IReadOnlyList<TelemetryEvent> GetQueuedEvents(int limit = 100)
    {
        var result = new System.Collections.Generic.List<TelemetryEvent>();
        if (!DatabaseExists) return result;

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, category, name, at, payload FROM telemetry_queue ORDER BY at DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new TelemetryEvent(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                System.DateTimeOffset.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
                reader.GetString(4)
            ));
        }
        return result;
    }

    public void ClearQueue()
    {
        if (!DatabaseExists) return;
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM telemetry_queue";
        cmd.ExecuteNonQuery();
    }

    private static readonly System.Collections.Generic.HashSet<string> AllowedFields = new(System.StringComparer.Ordinal)
    {
        "version", "channel", "command", "count", "duration", "exitCode",
        "adapter", "nookType", "action", "result", "error", "reason",
        "bayCount", "nookCount", "shoreCount", "agentCount",
        "themeName", "fontSize", "timestamp", "sessionDuration"
    };

    private static bool IsAllowedField(string key) => AllowedFields.Contains(key);
    private static bool IsAllowedPrimitive(object? value)
    {
        return value is int or long or float or double or bool or string or System.DateTimeOffset or System.DateTime;
    }

    private void EnsureSchema()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS telemetry_queue (
                id TEXT PRIMARY KEY,
                category TEXT NOT NULL,
                name TEXT NOT NULL,
                at TEXT NOT NULL,
                payload TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_telemetry_at ON telemetry_queue (at DESC);
            CREATE TABLE IF NOT EXISTS cli_usage_rollup (
                date TEXT NOT NULL,
                command TEXT NOT NULL,
                count INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (date, command)
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static string SerializePayload(System.Collections.Generic.IReadOnlyDictionary<string, object> payload)
    {
        using var ms = new System.IO.MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            foreach (var kv in payload)
            {
                writer.WritePropertyName(kv.Key);
                WriteJsonValue(writer, kv.Value);
            }
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteJsonValue(System.Text.Json.Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case int i:
                writer.WriteNumberValue(i);
                break;
            case long l:
                writer.WriteNumberValue(l);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case float f:
                writer.WriteNumberValue(f);
                break;
            case System.DateTimeOffset dto:
                writer.WriteStringValue(dto.ToString("o"));
                break;
            case System.DateTime dt:
                writer.WriteStringValue(dt.ToString("o"));
                break;
            default:
                writer.WriteStringValue(value?.ToString() ?? "");
                break;
        }
    }
}
