using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cove.Persistence;

public enum SplitOrientation { Row, Column }

[JsonConverter(typeof(PaneTypeConverter))]
public enum PaneType
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
[JsonDerivedType(typeof(PaneLeaf), "leaf")]
public abstract record MosaicNode;

public sealed record SplitNode : MosaicNode
{
    public required SplitOrientation Orientation { get; init; }
    private readonly double _ratio = 0.5;
    public double Ratio { get => _ratio == 0 ? 0.5 : _ratio; init => _ratio = value; }
    public required MosaicNode ChildA { get; init; }
    public required MosaicNode ChildB { get; init; }
}

public sealed record PaneLeaf : MosaicNode
{
    public required string PaneId { get; init; }
    private readonly IReadOnlyList<Subtab>? _subtabs;
    public IReadOnlyList<Subtab> Subtabs { get => _subtabs ?? System.Array.Empty<Subtab>(); init => _subtabs = value; }
    public int ActiveSubtab { get; init; }
}

public sealed record Subtab(string DocumentId, PaneType PaneType, string? Title = null);

public sealed record PaneDescriptor(string PaneId, string Command, string[] Args, string Cwd, string? Title = null, string? Adapter = null, string? AgentName = null, string? SessionId = null);

public sealed record RoomSnapshot
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required MosaicNode LayoutTree { get; init; }
    public string? ZoomedPaneId { get; init; }
}

public sealed record WorkspaceSnapshot
{
    private readonly int _schemaVersion = 1;
    public int SchemaVersion { get => _schemaVersion == 0 ? 1 : _schemaVersion; init => _schemaVersion = value; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ProjectDir { get; init; }
    public string? ActiveRoomId { get; init; }
    private readonly IReadOnlyList<RoomSnapshot>? _rooms;
    public IReadOnlyList<RoomSnapshot> Rooms { get => _rooms ?? System.Array.Empty<RoomSnapshot>(); init => _rooms = value; }
}

public sealed class PaneTypeConverter : JsonConverter<PaneType>
{
    private static readonly KeyValuePair<string, PaneType>[] s_map =
    {
        new("terminal", PaneType.Terminal),
        new("empty", PaneType.Empty),
        new("editor", PaneType.Editor),
        new("markdown", PaneType.Markdown),
        new("search", PaneType.Search),
        new("sourceControl", PaneType.SourceControl),
        new("browser", PaneType.Browser),
        new("image", PaneType.Image),
        new("diff", PaneType.Diff),
        new("pdf", PaneType.Pdf),
        new("video", PaneType.Video),
        new("achievements", PaneType.Achievements),
        new("tasks-list", PaneType.Tasks),
        new("notepad", PaneType.Notepad),
    };

    public override PaneType Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        var token = reader.GetString();
        if (token is null)
            throw new JsonException("paneType cannot be null");
        var normalized = token.Trim().ToLowerInvariant();
        if (normalized == "vanilla")
            return PaneType.Terminal;
        foreach (var pair in s_map)
        {
            if (string.Equals(pair.Key, normalized, System.StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        }
        throw new JsonException($"unknown paneType: {token}");
    }

    public override void Write(Utf8JsonWriter writer, PaneType value, JsonSerializerOptions options)
    {
        foreach (var pair in s_map)
        {
            if (pair.Value == value)
            {
                writer.WriteStringValue(pair.Key);
                return;
            }
        }
        throw new JsonException($"unserializable paneType: {value}");
    }
}
