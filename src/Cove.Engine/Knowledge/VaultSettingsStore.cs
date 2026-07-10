using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed class VaultSettings
{
    private string? _depth;
    public string Depth { get => _depth ?? "standard"; set => _depth = value; }
    private int? _horizon;
    public int Horizon { get => _horizon ?? 30; set => _horizon = value; }
    public string? ExtractorVersion { get; set; }
}

public sealed class VaultSettingsStore
{
    private readonly string _path;
    private readonly ILogger _logger;
    private readonly object _lock = new();
    private VaultSettings? _cached;

    public VaultSettingsStore(string dataDir, ILogger logger)
    {
        _path = System.IO.Path.Combine(dataDir, "vault", "settings.json");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
        _logger = logger;
    }

    public VaultSettings Get()
    {
        lock (_lock)
        {
            if (_cached is not null) return _cached;
            if (System.IO.File.Exists(_path))
            {
                try
                {
                    _cached = JsonSerializer.Deserialize(System.IO.File.ReadAllText(_path), VaultSettingsJsonContext.Default.VaultSettings);
                }
                catch (System.Exception ex)
                {
                    _logger.LogWarning("vault-settings: load failed: {err}", ex.Message);
                }
            }
            _cached ??= new VaultSettings();
            return _cached;
        }
    }

    public void Set(string key, string value)
    {
        lock (_lock)
        {
            var settings = Get();
            switch (key.ToLowerInvariant())
            {
                case "depth":
                    settings.Depth = value;
                    break;
                case "horizon":
                    if (int.TryParse(value, out var h)) settings.Horizon = h;
                    break;
                case "extractorversion":
                    settings.ExtractorVersion = value;
                    break;
                default:
                    _logger.LogWarning("vault-settings: unknown key {key}", key);
                    return;
            }
            System.IO.File.WriteAllText(_path, JsonSerializer.Serialize(settings, VaultSettingsJsonContext.Default.VaultSettings));
            _logger.LogWarning("vault-settings: set {key}={value}", key, value);
        }
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(VaultSettings))]
public sealed partial class VaultSettingsJsonContext : JsonSerializerContext { }
