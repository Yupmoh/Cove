using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cove.Persistence;

public enum SplitOrientation { Row, Column }

[JsonConverter(typeof(NookTypeConverter))]
public enum NookType
{
    Terminal,
    Empty,
    Editor,
    Markdown,
    Search,
    SourceControl,
    Browser,
    Image,
    Diff,
    Pdf,
    Video,
    Achievements,
    Tasks,
    Notepad,
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SplitNode), "split")]
[JsonDerivedType(typeof(NookLeaf), "leaf")]
public abstract record MosaicNode;

public sealed record SplitNode : MosaicNode
{
    public required SplitOrientation Orientation { get; init; }
    private readonly double _ratio = 0.5;
    public double Ratio { get => _ratio == 0 ? 0.5 : _ratio; init => _ratio = value; }
    public required MosaicNode ChildA { get; init; }
    public required MosaicNode ChildB { get; init; }
}

public sealed record NookLeaf : MosaicNode
{
    public required string NookId { get; init; }
    private readonly IReadOnlyList<Subtab>? _subtabs;
    public IReadOnlyList<Subtab> Subtabs { get => _subtabs ?? System.Array.Empty<Subtab>(); init => _subtabs = value; }
    public int ActiveSubtab { get; init; }
}

public sealed record Subtab(string DocumentId, NookType NookType, string? Title = null);

public sealed record NookDescriptor(string NookId, string Command, string[] Args, string Cwd, string? Title = null, string? Adapter = null, string? AgentName = null, string? SessionId = null, bool Yolo = false);

public sealed record ShoreSnapshot
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required MosaicNode LayoutTree { get; init; }
    public string? ZoomedNookId { get; init; }
}

public sealed record BaySnapshot
{
    private readonly int _schemaVersion = 1;
    public int SchemaVersion { get => _schemaVersion == 0 ? 1 : _schemaVersion; init => _schemaVersion = value; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ProjectDir { get; init; }
    public string? ActiveShoreId { get; init; }
    private readonly IReadOnlyList<ShoreSnapshot>? _shores;
    public IReadOnlyList<ShoreSnapshot> Shores { get => _shores ?? System.Array.Empty<ShoreSnapshot>(); init => _shores = value; }
}

public sealed class NookTypeConverter : JsonConverter<NookType>
{
    private static readonly KeyValuePair<string, NookType>[] s_map =
    {
        new("terminal", NookType.Terminal),
        new("empty", NookType.Empty),
        new("editor", NookType.Editor),
        new("markdown", NookType.Markdown),
        new("search", NookType.Search),
        new("sourceControl", NookType.SourceControl),
        new("browser", NookType.Browser),
        new("image", NookType.Image),
        new("diff", NookType.Diff),
        new("pdf", NookType.Pdf),
        new("video", NookType.Video),
        new("achievements", NookType.Achievements),
        new("tasks-list", NookType.Tasks),
        new("notepad", NookType.Notepad),
    };

    public override NookType Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        var token = reader.GetString();
        if (token is null)
            throw new JsonException("nookType cannot be null");
        var normalized = token.Trim().ToLowerInvariant();
        if (normalized == "vanilla")
            return NookType.Terminal;
        foreach (var pair in s_map)
        {
            if (string.Equals(pair.Key, normalized, System.StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        }
        throw new JsonException($"unknown nookType: {token}");
    }

    public override void Write(Utf8JsonWriter writer, NookType value, JsonSerializerOptions options)
    {
        foreach (var pair in s_map)
        {
            if (pair.Value == value)
            {
                writer.WriteStringValue(pair.Key);
                return;
            }
        }
        throw new JsonException($"unserializable nookType: {value}");
    }
}
