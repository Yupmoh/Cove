using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ZLogger;
namespace Cove.Engine.Activity;

public sealed record OmniChatMessage(string Role, string Body, DateTimeOffset SentAt);

public sealed class OmniChatStore
{
    private readonly string _root;
    private readonly ILogger? _logger;

    public OmniChatStore(string root, ILogger? logger = null)
    {
        _root = root;
        _logger = logger;
    }

    public void Append(string nookId, OmniChatMessage message)
    {
        if (!IsValidNookId(nookId))
        {
            _logger?.OmniChatAppendRejectedInvalidNookId(nookId);
            return;
        }
        Directory.CreateDirectory(_root);
        var path = GetPath(nookId);
        var messages = LoadRaw(nookId);
        messages.Add(message);
        var json = JsonSerializer.Serialize(messages, OmniChatJsonContext.Default.ListOmniChatMessage);
        File.WriteAllText(path, json);
    }

    public IReadOnlyList<OmniChatMessage> LoadHistory(string nookId)
    {
        if (!IsValidNookId(nookId))
        {
            _logger?.OmniChatLoadRejectedInvalidNookId(nookId);
            return Array.Empty<OmniChatMessage>();
        }
        return LoadRaw(nookId);
    }

    public void Clear(string nookId)
    {
        if (!IsValidNookId(nookId))
        {
            _logger?.OmniChatClearRejectedInvalidNookId(nookId);
            return;
        }
        var path = GetPath(nookId);
        if (File.Exists(path))
        {
            try { File.Delete(path); }
            catch (IOException ex) { _logger?.OmniChatClearFailed(nookId, ex.Message); }
        }
    }

    private List<OmniChatMessage> LoadRaw(string nookId)
    {
        var path = GetPath(nookId);
        if (!File.Exists(path))
            return new List<OmniChatMessage>();
        try
        {
            var json = File.ReadAllText(path);
            var messages = JsonSerializer.Deserialize(json, OmniChatJsonContext.Default.ListOmniChatMessage);
            if (messages is null)
                return new List<OmniChatMessage>();
            return messages.OrderBy(m => m.SentAt).ToList();
        }
        catch (JsonException ex)
        {
            _logger?.OmniChatLoadFailed(nookId, ex.Message);
            return new List<OmniChatMessage>();
        }
    }

    private static bool IsValidNookId(string nookId) =>
        !string.IsNullOrEmpty(nookId) && nookId.All(c => char.IsLetterOrDigit(c) || c == '-');

    private string GetPath(string nookId) => Path.Combine(_root, nookId + ".json");
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(List<OmniChatMessage>))]
public sealed partial class OmniChatJsonContext : JsonSerializerContext { }

internal static partial class OmniChatLog
{
    [ZLoggerMessage(LogLevel.Warning, "omni chat append rejected invalid nookId={nookId}")]
    public static partial void OmniChatAppendRejectedInvalidNookId(this ILogger logger, string nookId);

    [ZLoggerMessage(LogLevel.Warning, "omni chat load failed nookId={nookId} error={error}")]
    public static partial void OmniChatLoadFailed(this ILogger logger, string nookId, string error);
    [ZLoggerMessage(LogLevel.Warning, "omni chat load rejected invalid nookId={nookId}")]
    public static partial void OmniChatLoadRejectedInvalidNookId(this ILogger logger, string nookId);
    [ZLoggerMessage(LogLevel.Warning, "omni chat clear rejected invalid nookId={nookId}")]
    public static partial void OmniChatClearRejectedInvalidNookId(this ILogger logger, string nookId);

    [ZLoggerMessage(LogLevel.Warning, "omni chat clear failed nookId={nookId} error={error}")]
    public static partial void OmniChatClearFailed(this ILogger logger, string nookId, string error);
}
