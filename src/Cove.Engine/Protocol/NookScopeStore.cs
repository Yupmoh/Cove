using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Persistence;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Protocol;

public sealed class NookScopeStore
{
    private readonly string _path;
    private readonly ILogger? _logger;
    private readonly object _lock = new();
    private Dictionary<string, McpScope> _scopes = new();

    public NookScopeStore(string dataDir, ILogger? logger = null)
    {
        _path = Path.Combine(dataDir, "nook-scopes.json");
        _logger = logger;
        Load();
    }

    public McpScope GetScope(string nookId)
    {
        lock (_lock)
            return _scopes.TryGetValue(nookId, out var s) ? s : McpScope.SameBay;
    }

    public void SetScope(string nookId, McpScope scope)
    {
        lock (_lock)
        {
            _scopes[nookId] = scope;
            Save();
        }
    }

    public void ClearScope(string nookId)
    {
        lock (_lock)
        {
            if (!_scopes.Remove(nookId))
                return;
            Save();
        }
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var json = File.ReadAllText(_path);
            _scopes = JsonSerializer.Deserialize(json, NookScopeJsonContext.Default.DictionaryStringMcpScope) ?? new();
        }
        catch (JsonException ex)
        {
            _logger?.NookScopeLoadFailed(ex.Message);
            _scopes = new();
        }
        catch (IOException ex)
        {
            _logger?.NookScopeLoadFailed(ex.Message);
            _scopes = new();
        }
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        AtomicJsonStore.WriteRawText(_path, JsonSerializer.Serialize(_scopes, NookScopeJsonContext.Default.DictionaryStringMcpScope));
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, McpScope>))]
public sealed partial class NookScopeJsonContext : JsonSerializerContext { }

internal static partial class NookScopeLog
{
    [ZLoggerMessage(LogLevel.Warning, "nook scope load failed error={error}")]
    public static partial void NookScopeLoadFailed(this ILogger logger, string error);
}
