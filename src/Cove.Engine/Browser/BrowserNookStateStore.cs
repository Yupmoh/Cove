using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Browser;

public sealed record BrowserNookState(
    string NookId,
    string Url,
    string Title,
    string Favicon,
    string[] BackStack,
    string[] ForwardStack,
    System.Collections.Generic.Dictionary<string, double> PerSiteZoom,
    bool Incognito,
    string EngineTargetId,
    System.DateTimeOffset CreatedAt);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BrowserNookState))]
public sealed partial class BrowserNookStateJsonContext : JsonSerializerContext { }

public sealed class BrowserNookStateStore
{
    private readonly string _dataDir;
    private readonly ILogger _logger;

    public BrowserNookStateStore(string dataDir, ILogger logger)
    {
        _dataDir = dataDir;
        _logger = logger;
    }

    public void Save(string nookId, BrowserNookState state)
    {
        var dir = Path.Combine(_dataDir, "nooks", nookId);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "browser-state.json");
        var json = JsonSerializer.Serialize(state, BrowserNookStateJsonContext.Default.BrowserNookState);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, true);
    }

    public BrowserNookState? Load(string nookId)
    {
        var path = Path.Combine(_dataDir, "nooks", nookId, "browser-state.json");
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, BrowserNookStateJsonContext.Default.BrowserNookState);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "browser: failed to parse nook state for {nookId}", nookId);
            return null;
        }
    }

    public bool Delete(string nookId)
    {
        var path = Path.Combine(_dataDir, "nooks", nookId, "browser-state.json");
        if (!File.Exists(path)) return false;
        try { File.Delete(path); return true; }
        catch (System.Exception ex) { _logger.LogWarning(ex, "browser: failed to delete nook state for {nookId}", nookId); return false; }
    }
}
