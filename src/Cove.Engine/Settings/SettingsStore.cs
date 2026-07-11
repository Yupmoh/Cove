using System.Text.Json;
namespace Cove.Engine.Settings;

public sealed class SettingsStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, string> _values;

    public SettingsStore(string dataDir)
    {
        _path = Path.Combine(dataDir, "settings.json");
        _values = Load();
    }

    public string Get(string key, string defaultValue)
    {
        lock (_lock)
            return _values.TryGetValue(key, out var v) ? v : defaultValue;
    }

    public int GetInt(string key, int defaultValue) =>
        int.TryParse(Get(key, ""), out var v) ? v : defaultValue;

    public bool GetBool(string key, bool defaultValue) =>
        bool.TryParse(Get(key, ""), out var v) ? v : defaultValue;

    public void Set(string key, string value)
    {
        lock (_lock)
        {
            _values[key] = value;
            Save();
        }
    }

    public void Delete(string key)
    {
        lock (_lock)
        {
            _values.Remove(key);
            Save();
        }
    }

    public bool HasKey(string key)
    {
        lock (_lock)
            return _values.ContainsKey(key);
    }

    public IReadOnlyDictionary<string, string> GetAll()
    {
        lock (_lock)
            return new Dictionary<string, string>(_values);
    }

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
        catch { return new Dictionary<string, string>(); }
    }

    private void Save()
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (var kv in _values)
                writer.WriteString(kv.Key, kv.Value);
            writer.WriteEndObject();
            writer.Flush();
        }
        File.WriteAllText(_path, System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
    }
}

public sealed record Theme(string Name, string Background, string Foreground, string Accent, string Cursor);

public static class ThemeStore
{
    public static Theme GetDefault() => new("dark", "#0b1622", "#e5e9f0", "#4cc2d6", "#4cc2d6");

    public static IReadOnlyList<Theme> GetBuiltinThemes() => new List<Theme>
    {
        new("dark", "#0b1622", "#e5e9f0", "#4cc2d6", "#4cc2d6"),
        new("light", "#ffffff", "#1a1a2e", "#2b6d7a", "#2b6d7a"),
        new("latte", "#f5f0e8", "#3b3247", "#883955", "#883955"),
    };
}

public sealed class KeybindingStore
{
    private readonly SettingsStore _settings;

    public KeybindingStore(string dataDir)
    {
        _settings = new SettingsStore(dataDir);
    }

    public string Get(string action, string defaultValue)
    {
        var value = _settings.Get($"keybinding.{action}", "");
        if (!string.IsNullOrEmpty(value))
            return value;
        return BuiltinDefault(action) ?? defaultValue;
    }

    public void Set(string action, string binding)
    {
        _settings.Set($"keybinding.{action}", binding);
    }

    private static string? BuiltinDefault(string action) => action switch
    {
        "nook.newTerminal" => "cmd+t",
        "nook.splitHorizontal" => "cmd+d",
        "nook.splitVertical" => "cmd+shift+d",
        "nook.close" => "cmd+w",
        "nook.zoom" => "cmd+z",
        "sidebar.toggle" => "cmd+b",
        "palette.open" => "cmd+k",
        "launcher.open" => "cmd+l",
        "settings.open" => "cmd+,",
        "find.open" => "cmd+f",
        "zen.toggle" => "cmd+shift+`",
        _ => null,
    };
}
