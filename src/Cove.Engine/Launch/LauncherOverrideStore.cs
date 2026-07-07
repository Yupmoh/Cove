using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Engine.Restart;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Launch;

public sealed class LauncherOverrideStore
{
    private readonly string _root;
    private readonly ILogger? _logger;

    public LauncherOverrideStore(string root, ILogger? logger = null)
    {
        _root = root;
        _logger = logger;
    }

    public void Save(string paneId, LauncherOverrides overrides)
    {
        if (!IsValidPaneId(paneId))
        {
            _logger?.LauncherOverrideSaveRejectedInvalidPaneId(paneId);
            return;
        }
        Directory.CreateDirectory(_root);
        var path = GetPath(paneId);
        var json = JsonSerializer.Serialize(overrides, LauncherOverridePersistenceJsonContext.Default.LauncherOverrides);
        File.WriteAllText(path, json);
    }

    public bool TryLoad(string paneId, out LauncherOverrides? overrides)
    {
        if (!IsValidPaneId(paneId))
        {
            overrides = null;
            return false;
        }
        var path = GetPath(paneId);
        if (!File.Exists(path))
        {
            overrides = null;
            return false;
        }
        try
        {
            var json = File.ReadAllText(path);
            overrides = JsonSerializer.Deserialize(json, LauncherOverridePersistenceJsonContext.Default.LauncherOverrides);
            return overrides is not null;
        }
        catch (JsonException ex)
        {
            _logger?.LauncherOverrideLoadFailed(paneId, ex.Message);
            overrides = null;
            return false;
        }
    }

    public void Delete(string paneId)
    {
        if (!IsValidPaneId(paneId))
            return;
        var path = GetPath(paneId);
        if (File.Exists(path))
        {
            try { File.Delete(path); }
            catch (IOException ex) { _logger?.LauncherOverrideDeleteFailed(paneId, ex.Message); }
        }
    }

    public IReadOnlyDictionary<string, LauncherOverrides> LoadAll()
    {
        var result = new Dictionary<string, LauncherOverrides>();
        if (!Directory.Exists(_root))
            return result;
        foreach (var file in Directory.EnumerateFiles(_root, "*.json"))
        {
            var paneId = Path.GetFileNameWithoutExtension(file);
            try
            {
                var json = File.ReadAllText(file);
                var overrides = JsonSerializer.Deserialize(json, LauncherOverridePersistenceJsonContext.Default.LauncherOverrides);
                if (overrides is not null)
                    result[paneId] = overrides;
            }
            catch (JsonException ex)
            {
                _logger?.LauncherOverrideLoadFailed(paneId, ex.Message);
            }
        }
        return result;
    }

    private static bool IsValidPaneId(string paneId) =>
        !string.IsNullOrEmpty(paneId) && paneId.All(c => char.IsLetterOrDigit(c) || c == '-');

    private string GetPath(string paneId) => Path.Combine(_root, paneId + ".json");
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LauncherOverrides))]
public sealed partial class LauncherOverridePersistenceJsonContext : JsonSerializerContext { }

internal static partial class LauncherOverrideLog
{
    [ZLoggerMessage(LogLevel.Warning, "launcher override save rejected invalid paneId={paneId}")]
    public static partial void LauncherOverrideSaveRejectedInvalidPaneId(this ILogger logger, string paneId);

    [ZLoggerMessage(LogLevel.Warning, "launcher override load failed paneId={paneId} error={error}")]
    public static partial void LauncherOverrideLoadFailed(this ILogger logger, string paneId, string error);

    [ZLoggerMessage(LogLevel.Warning, "launcher override delete failed paneId={paneId} error={error}")]
    public static partial void LauncherOverrideDeleteFailed(this ILogger logger, string paneId, string error);
}
