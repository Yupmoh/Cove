using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Cove.Engine.Hooks;

public interface IAmbientContextProvider
{
    JsonElement Build(string? paneId);
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

    public JsonElement Build(string? paneId)
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
    private readonly System.Func<string?> _room;
    private readonly System.Func<string?> _wing;
    private readonly System.Func<string?> _workspace;
    private readonly System.Func<IReadOnlyList<string?>> _otherPanes;

    public LocationContextProvider(System.Func<string?> room, System.Func<string?> wing, System.Func<string?> workspace, System.Func<IReadOnlyList<string?>> otherPanes)
    {
        _room = room;
        _wing = wing;
        _workspace = workspace;
        _otherPanes = otherPanes;
    }

    public JsonElement Build(string? paneId)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            var room = _room();
            if (room is not null)
                writer.WriteString("room", room);
            var wing = _wing();
            if (wing is not null)
                writer.WriteString("wing", wing);
            var workspace = _workspace();
            if (workspace is not null)
                writer.WriteString("workspace", workspace);
            if (paneId is not null)
                writer.WriteString("paneId", paneId);
            writer.WriteStartArray("panes");
            foreach (var pane in _otherPanes())
            {
                if (pane is not null)
                    writer.WriteStringValue(pane);
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

    public JsonElement Build(string? paneId)
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

    public JsonElement? Get(string key, string? paneId = null)
    {
        return _providers.TryGetValue(key, out var provider) ? provider.Build(paneId) : null;
    }
}
