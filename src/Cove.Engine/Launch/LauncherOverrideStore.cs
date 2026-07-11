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

    public void Save(string nookId, LauncherOverrides overrides)
    {
        if (!IsValidNookId(nookId))
        {
            _logger?.LauncherOverrideSaveRejectedInvalidNookId(nookId);
            return;
        }
        Directory.CreateDirectory(_root);
        var path = GetPath(nookId);
        var json = JsonSerializer.Serialize(overrides, LauncherOverridePersistenceJsonContext.Default.LauncherOverrides);
        File.WriteAllText(path, json);
    }

    public bool TryLoad(string nookId, out LauncherOverrides? overrides)
    {
        if (!IsValidNookId(nookId))
        {
            overrides = null;
            return false;
        }
        var path = GetPath(nookId);
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
            _logger?.LauncherOverrideLoadFailed(nookId, ex.Message);
            overrides = null;
            return false;
        }
    }

    public void Delete(string nookId)
    {
        if (!IsValidNookId(nookId))
            return;
        var path = GetPath(nookId);
        if (File.Exists(path))
        {
            try { File.Delete(path); }
            catch (IOException ex) { _logger?.LauncherOverrideDeleteFailed(nookId, ex.Message); }
        }
    }

    public IReadOnlyDictionary<string, LauncherOverrides> LoadAll()
    {
        var result = new Dictionary<string, LauncherOverrides>();
        if (!Directory.Exists(_root))
            return result;
        foreach (var file in Directory.EnumerateFiles(_root, "*.json"))
        {
            var nookId = Path.GetFileNameWithoutExtension(file);
            try
            {
                var json = File.ReadAllText(file);
                var overrides = JsonSerializer.Deserialize(json, LauncherOverridePersistenceJsonContext.Default.LauncherOverrides);
                if (overrides is not null)
                    result[nookId] = overrides;
            }
            catch (JsonException ex)
            {
                _logger?.LauncherOverrideLoadFailed(nookId, ex.Message);
            }
        }
        return result;
    }

    private static bool IsValidNookId(string nookId) =>
        !string.IsNullOrEmpty(nookId) && nookId.All(c => char.IsLetterOrDigit(c) || c == '-');

    private string GetPath(string nookId) => Path.Combine(_root, nookId + ".json");
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LauncherOverrides))]
public sealed partial class LauncherOverridePersistenceJsonContext : JsonSerializerContext { }

internal static partial class LauncherOverrideLog
{
    [ZLoggerMessage(LogLevel.Warning, "launcher override save rejected invalid nookId={nookId}")]
    public static partial void LauncherOverrideSaveRejectedInvalidNookId(this ILogger logger, string nookId);

    [ZLoggerMessage(LogLevel.Warning, "launcher override load failed nookId={nookId} error={error}")]
    public static partial void LauncherOverrideLoadFailed(this ILogger logger, string nookId, string error);

    [ZLoggerMessage(LogLevel.Warning, "launcher override delete failed nookId={nookId} error={error}")]
    public static partial void LauncherOverrideDeleteFailed(this ILogger logger, string nookId, string error);
}
