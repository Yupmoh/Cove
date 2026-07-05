using System.Text.Json.Serialization;
using Cove.Persistence;

namespace Cove.Engine.Workspaces;

public sealed record PaneRecord
{
    public required string PaneId { get; init; }
    public PaneType PaneType { get; init; } = PaneType.Terminal;
    public string Cwd { get; init; } = "";
    public string Name { get; init; } = "";
}

public sealed record Wing
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}

public sealed record Room
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string WingId { get; init; } = WorkspaceModel.MainWingId;
    public string? ActivePaneId { get; init; }
    public required MosaicNode LayoutTree { get; init; }
    public string? ZoomedPaneId { get; init; }
}

public sealed record WorkspaceModel
{
    public const string MainWingId = "main";
    public const string DefaultCollectionId = "default";

    public int SchemaVersion { get; init; } = 1;
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ProjectDir { get; init; }
    public string CollectionId { get; init; } = DefaultCollectionId;
    public bool IsWorktree { get; init; }
    public string? ParentWorkspaceId { get; init; }
    public string? WorktreeBranch { get; init; }
    public string? FocusedPaneId { get; init; }
    public string? ActiveRoomId { get; init; }
    public IReadOnlyList<Wing> Wings { get; init; } = [];
    public IReadOnlyList<Room> Rooms { get; init; } = [];
    public IReadOnlyDictionary<string, PaneRecord> Panes { get; init; } = new Dictionary<string, PaneRecord>();
}

public sealed record Collection
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}

public sealed record RegistryModel
{
    public int SchemaVersion { get; init; } = 1;
    public string? FocusedWorkspaceId { get; init; }
    public IReadOnlyList<string> OpenWorkspaces { get; init; } = [];
    public IReadOnlyList<Collection> Collections { get; init; } = [];
    public string ActiveCollectionId { get; init; } = WorkspaceModel.DefaultCollectionId;
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WorkspaceModel))]
[JsonSerializable(typeof(RegistryModel))]
[JsonSerializable(typeof(Room))]
[JsonSerializable(typeof(Wing))]
[JsonSerializable(typeof(PaneRecord))]
[JsonSerializable(typeof(Collection))]
[JsonSerializable(typeof(MosaicNode))]
[JsonSerializable(typeof(SplitNode))]
[JsonSerializable(typeof(PaneLeaf))]
public sealed partial class WorkspacesJsonContext : JsonSerializerContext { }
