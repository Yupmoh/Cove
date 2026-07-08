using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Browser;

public sealed record BrowserPaneState(
    string PaneId,
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
[JsonSerializable(typeof(BrowserPaneState))]
public sealed partial class BrowserPaneStateJsonContext : JsonSerializerContext { }

public sealed class BrowserPaneStateStore
{
    private readonly string _dataDir;
    private readonly ILogger _logger;

    public BrowserPaneStateStore(string dataDir, ILogger logger)
    {
        _dataDir = dataDir;
        _logger = logger;
    }

    public void Save(string paneId, BrowserPaneState state)
    {
        var dir = Path.Combine(_dataDir, "panes", paneId);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "browser-state.json");
        var json = JsonSerializer.Serialize(state, BrowserPaneStateJsonContext.Default.BrowserPaneState);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, true);
    }

    public BrowserPaneState? Load(string paneId)
    {
        var path = Path.Combine(_dataDir, "panes", paneId, "browser-state.json");
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, BrowserPaneStateJsonContext.Default.BrowserPaneState);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "browser: failed to parse pane state for {paneId}", paneId);
            return null;
        }
    }

    public bool Delete(string paneId)
    {
        var path = Path.Combine(_dataDir, "panes", paneId, "browser-state.json");
        if (!File.Exists(path)) return false;
        try { File.Delete(path); return true; }
        catch (System.Exception ex) { _logger.LogWarning(ex, "browser: failed to delete pane state for {paneId}", paneId); return false; }
    }
}
