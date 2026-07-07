using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Cove.Persistence;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Config;

public sealed class ConfigService
{
    private readonly string _path;
    private readonly ILogger _logger;
    private readonly object _lock = new();

    public ConfigService(string dataDir, ILogger logger)
    {
        _path = Path.Combine(dataDir, "config.json");
        _logger = logger;
    }

    public string? Get(string key)
    {
        var normalized = Normalize(key);
        lock (_lock)
        {
            var values = Load();
            return values.TryGetValue(normalized, out var v) ? v : null;
        }
    }

    public void Set(string key, string value)
    {
        var normalized = Normalize(key);
        lock (_lock)
        {
            var values = Load();
            values[normalized] = value;
            Save(values);
        }
    }

    private static string Normalize(string key) => key.Replace(".", ":");

    private Dictionary<string, string> Load()
    {
        if (!File.Exists(_path))
            return new Dictionary<string, string>();
        try
        {
            var json = File.ReadAllText(_path);
            using var doc = JsonDocument.Parse(json);
            var result = new Dictionary<string, string>();
            foreach (var prop in doc.RootElement.EnumerateObject())
                result[prop.Name] = prop.Value.ToString();
            return result;
        }
        catch (System.Exception ex)
        {
            _logger.ConfigParseFailed(_path, ex.Message);
            return new Dictionary<string, string>();
        }
    }

    private void Save(Dictionary<string, string> values)
        => AtomicJsonStore.WriteRawText(_path, JsonSerializer.Serialize(values, Cove.Protocol.CoveJsonContext.Default.DictionaryStringString));
}
