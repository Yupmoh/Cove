using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Persistence;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Protocol;

public sealed class PaneScopeStore
{
    private readonly string _path;
    private readonly ILogger? _logger;
    private readonly object _lock = new();
    private Dictionary<string, McpScope> _scopes = new();

    public PaneScopeStore(string dataDir, ILogger? logger = null)
    {
        _path = Path.Combine(dataDir, "pane-scopes.json");
        _logger = logger;
        Load();
    }

    public McpScope GetScope(string paneId)
    {
        lock (_lock)
            return _scopes.TryGetValue(paneId, out var s) ? s : McpScope.SameWorkspace;
    }

    public void SetScope(string paneId, McpScope scope)
    {
        lock (_lock)
        {
            _scopes[paneId] = scope;
            Save();
        }
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var json = File.ReadAllText(_path);
            _scopes = JsonSerializer.Deserialize(json, PaneScopeJsonContext.Default.DictionaryStringMcpScope) ?? new();
        }
        catch (JsonException ex)
        {
            _logger?.PaneScopeLoadFailed(ex.Message);
            _scopes = new();
        }
        catch (IOException ex)
        {
            _logger?.PaneScopeLoadFailed(ex.Message);
            _scopes = new();
        }
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        AtomicJsonStore.WriteRawText(_path, JsonSerializer.Serialize(_scopes, PaneScopeJsonContext.Default.DictionaryStringMcpScope));
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, McpScope>))]
public sealed partial class PaneScopeJsonContext : JsonSerializerContext { }

internal static partial class PaneScopeLog
{
    [ZLoggerMessage(LogLevel.Warning, "pane scope load failed error={error}")]
    public static partial void PaneScopeLoadFailed(this ILogger logger, string error);
}
