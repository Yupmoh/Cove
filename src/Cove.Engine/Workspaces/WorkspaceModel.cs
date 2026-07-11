using System.Text.Json.Serialization;
using Cove.Persistence;

namespace Cove.Engine.Workspaces;

public sealed record PaneRecord
{
    public required string PaneId { get; init; }
    public PaneType PaneType { get; init; } = PaneType.Terminal;
    public string Cwd { get; init; } = "";
    public string Name { get; init; } = "";
    private readonly string? _residentScope;
    public string ResidentScope { get => _residentScope ?? "none"; init => _residentScope = value; }
    public int ResidentSlot { get; init; } = -1;
    public bool ResidentCollapsed { get; init; }
    public int ResidentHeight { get; init; }
}

public sealed record Wing
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public WorkspaceIcon? Icon { get; init; }
}

public sealed record Room
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    private readonly string? _wingId;
    public string WingId { get => _wingId ?? WorkspaceModel.MainWingId; init => _wingId = value; }
    public string? ActivePaneId { get; init; }
    public required MosaicNode LayoutTree { get; init; }
    public string? ZoomedPaneId { get; init; }
    public bool Pinned { get; init; }
}

public sealed record WorkspaceModel
{
    public const string MainWingId = "main";
    public const string DefaultCollectionId = "default";

    private readonly int _schemaVersion = 1;
    public int SchemaVersion { get => _schemaVersion == 0 ? 1 : _schemaVersion; init => _schemaVersion = value; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ProjectDir { get; init; }
    private readonly string? _collectionId;
    public string CollectionId { get => _collectionId ?? DefaultCollectionId; init => _collectionId = value; }
    public bool IsWorktree { get; init; }
    public string? ParentWorkspaceId { get; init; }
    public string? WorktreeBranch { get; init; }
    public string? FocusedPaneId { get; init; }
    public string? ActiveRoomId { get; init; }
    private readonly IReadOnlyList<Wing>? _wings;
    public IReadOnlyList<Wing> Wings { get => _wings ?? []; init => _wings = value; }
    private readonly IReadOnlyList<Room>? _rooms;
    public IReadOnlyList<Room> Rooms { get => _rooms ?? []; init => _rooms = value; }
    private readonly IReadOnlyDictionary<string, PaneRecord>? _panes;
    public IReadOnlyDictionary<string, PaneRecord> Panes { get => _panes ?? new Dictionary<string, PaneRecord>(); init => _panes = value; }
    public bool Hidden { get; init; }
    public string? AccentColor { get; init; }
    public WorkspaceIcon? Icon { get; init; }
}

public sealed record WorkspaceIcon(string Kind, string Value);

public sealed record Collection
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}

public sealed record RegistryModel
{
    private readonly int _schemaVersion = 1;
    public int SchemaVersion { get => _schemaVersion == 0 ? 1 : _schemaVersion; init => _schemaVersion = value; }
    public string? FocusedWorkspaceId { get; init; }
    private readonly IReadOnlyList<string>? _openWorkspaces;
    public IReadOnlyList<string> OpenWorkspaces { get => _openWorkspaces ?? []; init => _openWorkspaces = value; }
    private readonly IReadOnlyList<Collection>? _collections;
    public IReadOnlyList<Collection> Collections { get => _collections ?? []; init => _collections = value; }
    private readonly string? _activeCollectionId;
    public string ActiveCollectionId { get => _activeCollectionId ?? WorkspaceModel.DefaultCollectionId; init => _activeCollectionId = value; }
}

public sealed record WorkspaceSummary(string Id, string Name, string ProjectDir, string CollectionId, bool IsWorktree, bool Active, bool Hidden = false, string? IconKind = null, string? IconValue = null);

public sealed record WorkspaceCreateParams(string Name, string ProjectDir, string? CollectionId = null);

public sealed record WorkspaceIdParams(string Id);

public sealed record WorkspaceIdResult(string Id);

public sealed record WorkspaceListResult(IReadOnlyList<WorkspaceSummary> Workspaces);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WorkspaceModel))]
[JsonSerializable(typeof(RegistryModel))]
[JsonSerializable(typeof(Room))]
[JsonSerializable(typeof(WorkspaceIcon))]
[JsonSerializable(typeof(Wing))]
[JsonSerializable(typeof(PaneRecord))]
[JsonSerializable(typeof(Collection))]
[JsonSerializable(typeof(MosaicNode))]
[JsonSerializable(typeof(SplitNode))]
[JsonSerializable(typeof(PaneLeaf))]
[JsonSerializable(typeof(WorkspaceSummary))]
[JsonSerializable(typeof(WorkspaceCreateParams))]
[JsonSerializable(typeof(WorkspaceIdParams))]
[JsonSerializable(typeof(WorkspaceIdResult))]
[JsonSerializable(typeof(WorkspaceListResult))]
public sealed partial class WorkspacesJsonContext : JsonSerializerContext { }
