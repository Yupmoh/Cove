using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed class ReviewReadyDismissalStore
{
    private readonly string _path;
    private readonly ILogger _logger;
    private readonly object _lock = new();
    private HashSet<string> _dismissed;

    public ReviewReadyDismissalStore(string dataDir, ILogger? logger = null)
    {
        _path = System.IO.Path.Combine(dataDir, "review-ready-dismissals.json");
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _dismissed = Load();
    }

    public bool IsDismissed(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogWarning("review-dismissal: key required");
            return false;
        }
        lock (_lock)
            return _dismissed.Contains(key);
    }

    public void Dismiss(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogWarning("review-dismissal: key required for dismiss");
            return;
        }
        lock (_lock)
        {
            if (_dismissed.Add(key))
            {
                Save();
                _logger.LogInformation("review-dismissal: dismissed key {key}", key);
            }
        }
    }

    public bool Restore(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogWarning("review-dismissal: key required for restore");
            return false;
        }
        lock (_lock)
        {
            if (_dismissed.Remove(key))
            {
                Save();
                _logger.LogInformation("review-dismissal: restored key {key}", key);
                return true;
            }
            return false;
        }
    }

    public IReadOnlyList<string> ListDismissed()
    {
        lock (_lock)
            return _dismissed.ToList();
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            if (_dismissed.Count > 0)
            {
                _dismissed.Clear();
                Save();
                _logger.LogInformation("review-dismissal: cleared all dismissed keys");
            }
        }
    }

    private HashSet<string> Load()
    {
        try
        {
            if (!System.IO.File.Exists(_path))
                return new HashSet<string>();
            var json = System.IO.File.ReadAllText(_path);
            var list = System.Text.Json.JsonSerializer.Deserialize(json, DismissalJsonContext.Default.ListString);
            return list is not null ? new HashSet<string>(list) : new HashSet<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "review-dismissal: failed to load from {path}", _path);
            return new HashSet<string>();
        }
    }

    private void Save()
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                System.IO.Directory.CreateDirectory(dir);
            var list = _dismissed.ToList();
            var json = System.Text.Json.JsonSerializer.Serialize(list, DismissalJsonContext.Default.ListString);
            var tmp = _path + ".tmp";
            System.IO.File.WriteAllText(tmp, json);
            System.IO.File.Move(tmp, _path, true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "review-dismissal: failed to save to {path}", _path);
        }
    }
}
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(System.Collections.Generic.List<string>))]
public sealed partial class DismissalJsonContext : JsonSerializerContext { }
