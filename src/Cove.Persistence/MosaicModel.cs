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
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SplitNode), "split")]
[JsonDerivedType(typeof(PaneLeaf), "leaf")]
public abstract record MosaicNode;

public sealed record SplitNode : MosaicNode
{
    public required SplitOrientation Orientation { get; init; }
    public double Ratio { get; init; } = 0.5;
    public required MosaicNode ChildA { get; init; }
    public required MosaicNode ChildB { get; init; }
}

public sealed record PaneLeaf : MosaicNode
{
    public required string PaneId { get; init; }
    public IReadOnlyList<Subtab> Subtabs { get; init; } = System.Array.Empty<Subtab>();
    public int ActiveSubtab { get; init; }
}

public sealed record Subtab(string DocumentId, PaneType PaneType, string? Title = null);

public sealed record PaneDescriptor(string PaneId, string Command, string[] Args, string Cwd, string? Title = null);

public sealed record RoomSnapshot
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required MosaicNode LayoutTree { get; init; }
    public string? ZoomedPaneId { get; init; }
}

public sealed record WorkspaceSnapshot
{
    public int SchemaVersion { get; init; } = 1;
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ProjectDir { get; init; }
    public string? ActiveRoomId { get; init; }
    public IReadOnlyList<RoomSnapshot> Rooms { get; init; } = System.Array.Empty<RoomSnapshot>();
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
