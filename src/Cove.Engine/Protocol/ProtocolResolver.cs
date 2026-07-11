using System.Text.Json;

namespace Cove.Engine.Protocol;

public sealed class ProtocolResolver
{
    public (string? Uri, JsonElement? Params) Resolve(string coveUri, string? focusedNookId, string? activeShoreId)
    {
        if (!coveUri.StartsWith("cove://", StringComparison.Ordinal))
            return (null, null);

        var path = coveUri["cove://".Length..];
        var queryIndex = path.IndexOf('?');
        var pathPart = queryIndex >= 0 ? path[..queryIndex] : path;
        var queryPart = queryIndex >= 0 ? path[(queryIndex + 1)..] : "";

        var segments = pathPart.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return (null, null);

        var category = segments[0];
        var queryParams = ParseQuery(queryPart, focusedNookId, activeShoreId);

        return category switch
        {
            "commands" => ResolveCommands(segments, queryParams),
            "nooks" => ResolveNooks(segments, queryParams),
            "agents" => ResolveAgents(segments, queryParams),
            "skills" => ResolveSkills(segments, queryParams),
            _ => (null, null),
        };
    }

    private static (string?, JsonElement?) ResolveCommands(string[] segments, Dictionary<string, string> queryParams)
    {
        var action = segments.Length > 1 ? string.Join('.', segments[1..]) : "";
        var uri = action switch
        {
            "nook.split.horizontal" or "nook.split.vertical" => "cove://commands/layout.mutate",
            "nook.close" => "cove://commands/nook.kill",
            "shore.new" => "cove://commands/shore.create",
            "launcher.open" => "cove://commands/launcher.open",
            _ => $"cove://commands/{action}",
        };
        return (uri, ToJson(queryParams));
    }

    private static (string?, JsonElement?) ResolveNooks(string[] segments, Dictionary<string, string> queryParams)
    {
        if (segments.Length == 1)
            return ("cove://commands/nook.list", null);

        var nookId = segments[1];
        if (segments.Length == 2)
        {
            queryParams["nookId"] = nookId;
            return ("cove://commands/nook.list", ToJson(queryParams));
        }

        var action = segments[2];
        queryParams["nookId"] = nookId;
        return action switch
        {
            "write" => ("cove://commands/nook.write", ToJson(queryParams)),
            "scrollback" => ("cove://commands/nook.scrollback", ToJson(queryParams)),
            _ => ($"cove://commands/nook.{action}", ToJson(queryParams)),
        };
    }

    private static (string?, JsonElement?) ResolveAgents(string[] segments, Dictionary<string, string> queryParams)
    {
        if (segments.Length == 1 || (segments.Length == 2 && segments[1] == "list"))
            return ("cove://commands/agent.list", ToJson(queryParams));

        if (segments.Length >= 3)
        {
            queryParams["target"] = segments[1];
            return segments[2] switch
            {
                "message" => ("cove://commands/agent.message", ToJson(queryParams)),
                "dismiss" => ("cove://commands/session.dismiss", ToJson(queryParams)),
                "wake" => ("cove://commands/session.wake", ToJson(queryParams)),
                _ => (null, null),
            };
        }

        return (null, null);
    }

    private static (string?, JsonElement?) ResolveSkills(string[] segments, Dictionary<string, string> queryParams)
    {
        if (segments.Length == 1 || segments[1] == "index")
            return ("cove://commands/skills.index", ToJson(queryParams));
        return (null, null);
    }

    private static Dictionary<string, string> ParseQuery(string query, string? focusedNookId, string? activeShoreId)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(query))
            return result;

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0) continue;
            var key = pair[..eq];
            var value = pair[(eq + 1)..];
            value = value switch
            {
                "$FOCUS" when focusedNookId is not null => focusedNookId,
                "$ACTIVE" when activeShoreId is not null => activeShoreId,
                _ => value,
            };
            result[key] = value;
        }
        return result;
    }

    private static JsonElement? ToJson(Dictionary<string, string> queryParams)
    {
        if (queryParams.Count == 0)
            return null;
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var kv in queryParams)
                writer.WriteString(kv.Key, kv.Value);
            writer.WriteEndObject();
            writer.Flush();
        }
        return JsonDocument.Parse(buffer.ToArray()).RootElement.Clone();
    }
}
