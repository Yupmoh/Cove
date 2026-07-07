using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Cove.Persistence;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Config;

public sealed class ConfigService : System.IDisposable
{
    private readonly string _path;
    private readonly ILogger _logger;
    private readonly object _lock = new();
    private Dictionary<string, string> _values = new();
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    public event Action<string>? SettingsChanged;

    public ConfigService(string dataDir, ILogger logger)
    {
        _path = Path.Combine(dataDir, "config.json");
        _logger = logger;
        Load();
    }

    public string? Get(string key)
    {
        lock (_lock)
            return _values.TryGetValue(key, out var v) ? v : null;
    }

    public void Set(string key, string value)
    {
        lock (_lock)
        {
            _values[key] = value;
            Save();
        }
        SettingsChanged?.Invoke(key);
    }

    public void StartWatching()
    {
        if (_watcher is not null) return;
        var dir = Path.GetDirectoryName(_path);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
        _watcher = new FileSystemWatcher(dir, Path.GetFileName(_path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Renamed += (sender, e) => OnFileChanged(sender, e);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        var changed = ReloadAndCollectChanges();
        foreach (var key in changed)
            SettingsChanged?.Invoke(key);
    }

    public void Reload()
    {
        var changed = ReloadAndCollectChanges();
        foreach (var key in changed)
            SettingsChanged?.Invoke(key);
    }

    private List<string> ReloadAndCollectChanges()
    {
        lock (_lock)
        {
            var before = new Dictionary<string, string>(_values);
            Load();
            var changed = new List<string>();
            foreach (var kv in _values)
                if (!before.TryGetValue(kv.Key, out var prev) || prev != kv.Value)
                    changed.Add(kv.Key);
            foreach (var key in before.Keys)
                if (!_values.ContainsKey(key))
                    changed.Add(key);
            return changed;
        }
    }
    private void Load()
    {
        if (!File.Exists(_path))
        {
            _values = new Dictionary<string, string>();
            return;
        }
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                var json = File.ReadAllText(_path);
                var result = JsonSerializer.Deserialize(json, Cove.Protocol.CoveJsonContext.Default.DictionaryStringString);
                _values = result ?? new Dictionary<string, string>();
                return;
            }
            catch (JsonException ex)
            {
                _logger.ConfigParseFailed(_path, ex.Message);
                _values = new Dictionary<string, string>();
                return;
            }
            catch (IOException)
            {
                System.Threading.Thread.Sleep(20);
            }
        }
        _logger.ConfigReadFailed(_path);
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        AtomicJsonStore.WriteRawText(_path, JsonSerializer.Serialize(_values, Cove.Protocol.CoveJsonContext.Default.DictionaryStringString));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_watcher is not null)
        {
            _watcher.Changed -= OnFileChanged;
            _watcher.Created -= OnFileChanged;
            _watcher.Dispose();
        }
    }
}
