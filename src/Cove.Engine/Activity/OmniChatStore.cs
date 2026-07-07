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

    public void Append(string paneId, OmniChatMessage message)
    {
        if (!IsValidPaneId(paneId))
        {
            _logger?.OmniChatAppendRejectedInvalidPaneId(paneId);
            return;
        }
        Directory.CreateDirectory(_root);
        var path = GetPath(paneId);
        var messages = LoadRaw(paneId);
        messages.Add(message);
        var json = JsonSerializer.Serialize(messages, OmniChatJsonContext.Default.ListOmniChatMessage);
        File.WriteAllText(path, json);
    }

    public IReadOnlyList<OmniChatMessage> LoadHistory(string paneId)
    {
        if (!IsValidPaneId(paneId))
            return Array.Empty<OmniChatMessage>();
        return LoadRaw(paneId);
    }

    public void Clear(string paneId)
    {
        if (!IsValidPaneId(paneId))
            return;
        var path = GetPath(paneId);
        if (File.Exists(path))
        {
            try { File.Delete(path); }
            catch (IOException ex) { _logger?.OmniChatClearFailed(paneId, ex.Message); }
        }
    }

    private List<OmniChatMessage> LoadRaw(string paneId)
    {
        var path = GetPath(paneId);
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
            _logger?.OmniChatLoadFailed(paneId, ex.Message);
            return new List<OmniChatMessage>();
        }
    }

    private static bool IsValidPaneId(string paneId) =>
        !string.IsNullOrEmpty(paneId) && paneId.All(c => char.IsLetterOrDigit(c) || c == '-');

    private string GetPath(string paneId) => Path.Combine(_root, paneId + ".json");
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(List<OmniChatMessage>))]
public sealed partial class OmniChatJsonContext : JsonSerializerContext { }

internal static partial class OmniChatLog
{
    [ZLoggerMessage(LogLevel.Warning, "omni chat append rejected invalid paneId={paneId}")]
    public static partial void OmniChatAppendRejectedInvalidPaneId(this ILogger logger, string paneId);

    [ZLoggerMessage(LogLevel.Warning, "omni chat load failed paneId={paneId} error={error}")]
    public static partial void OmniChatLoadFailed(this ILogger logger, string paneId, string error);

    [ZLoggerMessage(LogLevel.Warning, "omni chat clear failed paneId={paneId} error={error}")]
    public static partial void OmniChatClearFailed(this ILogger logger, string paneId, string error);
}
