using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Cove.Engine.Hooks;

public interface IAmbientContextProvider
{
    JsonElement Build(string? nookId);
}

public sealed class SessionStartContextProvider : IAmbientContextProvider
{
    private readonly System.Func<string> _primer;
    private readonly System.Func<string> _skillsManifest;
    private readonly System.Func<string> _agentPackaging;

    public SessionStartContextProvider(System.Func<string> primer, System.Func<string> skillsManifest, System.Func<string> agentPackaging)
    {
        _primer = primer;
        _skillsManifest = skillsManifest;
        _agentPackaging = agentPackaging;
    }

    public JsonElement Build(string? nookId)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("context", _primer());
            var skills = _skillsManifest();
            writer.WriteBoolean("skills", !string.IsNullOrEmpty(skills));
            if (!string.IsNullOrEmpty(skills))
                writer.WriteString("skillsManifest", skills);
            var agent = _agentPackaging();
            writer.WriteBoolean("agent", !string.IsNullOrEmpty(agent));
            if (!string.IsNullOrEmpty(agent))
                writer.WriteString("agentPackaging", agent);
            writer.WriteEndObject();
            writer.Flush();
        }
        return JsonDocument.Parse(buffer.ToArray()).RootElement.Clone();
    }
}

public sealed class LocationContextProvider : IAmbientContextProvider
{
    private readonly System.Func<string?> _shore;
    private readonly System.Func<string?> _wing;
    private readonly System.Func<string?> _bay;
    private readonly System.Func<IReadOnlyList<string?>> _otherNooks;

    public LocationContextProvider(System.Func<string?> shore, System.Func<string?> wing, System.Func<string?> bay, System.Func<IReadOnlyList<string?>> otherNooks)
    {
        _shore = shore;
        _wing = wing;
        _bay = bay;
        _otherNooks = otherNooks;
    }

    public JsonElement Build(string? nookId)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            var shore = _shore();
            if (shore is not null)
                writer.WriteString("shore", shore);
            var wing = _wing();
            if (wing is not null)
                writer.WriteString("wing", wing);
            var bay = _bay();
            if (bay is not null)
                writer.WriteString("bay", bay);
            if (nookId is not null)
                writer.WriteString("nookId", nookId);
            writer.WriteStartArray("nooks");
            foreach (var nook in _otherNooks())
            {
                if (nook is not null)
                    writer.WriteStringValue(nook);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
        }
        return JsonDocument.Parse(buffer.ToArray()).RootElement.Clone();
    }
}

public sealed class RunCommandContextProvider : IAmbientContextProvider
{
    private readonly System.Func<IReadOnlyList<string>> _runningCommands;

    public RunCommandContextProvider(System.Func<IReadOnlyList<string>> runningCommands)
    {
        _runningCommands = runningCommands;
    }

    public JsonElement Build(string? nookId)
    {
        var commands = _runningCommands();
        if (commands.Count == 0)
            return JsonDocument.Parse("{}").RootElement.Clone();

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("runningCommands");
            foreach (var cmd in commands)
                writer.WriteStringValue(cmd);
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
        }
        return JsonDocument.Parse(buffer.ToArray()).RootElement.Clone();
    }
}

public sealed class AmbientContextAggregator
{
    private readonly Dictionary<string, IAmbientContextProvider> _providers = new();

    public void Add(string key, IAmbientContextProvider provider) => _providers[key] = provider;

    public void Remove(string key) => _providers.Remove(key);

    public JsonElement? Get(string key, string? nookId = null)
    {
        return _providers.TryGetValue(key, out var provider) ? provider.Build(nookId) : null;
    }
}
