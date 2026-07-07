using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Protocol;

public sealed class StateBus
{
    private readonly string _path;
    private readonly ILogger? _logger;
    private readonly object _lock = new();
    private Dictionary<string, string?> _values = new();

    public event Action<string>? StateChanged;

    private static readonly HashSet<string> ValidScopes = new() { "app", "workspace", "tab", "pane" };

    public StateBus(string dataDir, ILogger? logger = null)
    {
        _path = Path.Combine(dataDir, "state.json");
        _logger = logger;
        Load();
    }

    public static bool IsValidScope(string scope) => ValidScopes.Contains(scope);

    public (bool Exists, string? Value) Read(string scope, string ns, string id)
    {
        var key = $"{scope}/{ns}/{id}";
        lock (_lock)
            return _values.TryGetValue(key, out var v) ? (true, v) : (false, null);
    }

    public void Write(string scope, string ns, string id, string? value)
    {
        var key = $"{scope}/{ns}/{id}";
        lock (_lock)
        {
            if (value is null)
                _values.Remove(key);
            else
                _values[key] = value;
            Save();
        }
        StateChanged?.Invoke(key);
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var json = File.ReadAllText(_path);
            _values = JsonSerializer.Deserialize(json, StateBusJsonContext.Default.DictionaryStringString) ?? new();
        }
        catch (JsonException) { _values = new(); }
        catch (IOException) { _values = new(); }
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_path, JsonSerializer.Serialize(_values, StateBusJsonContext.Default.DictionaryStringString));
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, string?>))]
public sealed partial class StateBusJsonContext : JsonSerializerContext { }
