using System.Text.Json;

namespace Cove.Engine.Hooks;

public interface IAmbientContextProvider
{
    JsonElement Build();
}

public sealed class SessionStartContextProvider : IAmbientContextProvider
{
    private readonly string _primer;
    private readonly string _skillsManifest;
    private readonly string _agentPackaging;

    public SessionStartContextProvider(string primer, string skillsManifest, string agentPackaging)
    {
        _primer = primer;
        _skillsManifest = skillsManifest;
        _agentPackaging = agentPackaging;
    }

    public JsonElement Build()
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("context", _primer);
            writer.WriteBoolean("skills", !string.IsNullOrEmpty(_skillsManifest));
            writer.WriteBoolean("agent", !string.IsNullOrEmpty(_agentPackaging));
            writer.WriteEndObject();
            writer.Flush();
        }
        return JsonDocument.Parse(buffer.ToArray()).RootElement.Clone();
    }
}

public sealed class LocationContextProvider : IAmbientContextProvider
{
    private readonly string _room;
    private readonly string? _wing;
    private readonly string _workspace;
    private readonly IReadOnlyList<string?> _otherPanes;

    public LocationContextProvider(string room, string? wing, string workspace, IReadOnlyList<string?> otherPanes)
    {
        _room = room;
        _wing = wing;
        _workspace = workspace;
        _otherPanes = otherPanes;
    }

    public JsonElement Build()
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("room", _room);
            if (_wing is not null)
                writer.WriteString("wing", _wing);
            writer.WriteString("workspace", _workspace);
            writer.WriteStartArray("panes");
            foreach (var pane in _otherPanes)
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
    private readonly IReadOnlyList<string> _runningCommands;

    public RunCommandContextProvider(IReadOnlyList<string> runningCommands)
    {
        _runningCommands = runningCommands;
    }

    public JsonElement Build()
    {
        if (_runningCommands.Count == 0)
            return JsonDocument.Parse("{}").RootElement.Clone();

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("runningCommands");
            foreach (var cmd in _runningCommands)
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

    public JsonElement? Get(string key)
    {
        return _providers.TryGetValue(key, out var provider) ? provider.Build() : null;
    }
}
