using System.Text.Json.Serialization;
using Cove.Persistence;

namespace Cove.Engine.Bays;

public sealed record NookRecord
{
    public required string NookId { get; init; }
    public NookType NookType { get; init; } = NookType.Terminal;
    public string Cwd { get; init; } = "";
    public string Name { get; init; } = "";
    private readonly string? _residentScope;
    public string ResidentScope { get => _residentScope ?? "none"; init => _residentScope = value; }
    public int ResidentSlot { get; init; } = -1;
    public bool ResidentCollapsed { get; init; }
    public int ResidentHeight { get; init; }
}

public sealed record BayModel
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
    public string? ParentBayId { get; init; }
    public string? WorktreeBranch { get; init; }
    private readonly IReadOnlyDictionary<string, NookRecord>? _nooks;
    public IReadOnlyDictionary<string, NookRecord> Nooks { get => _nooks ?? new Dictionary<string, NookRecord>(); init => _nooks = value; }
    public bool Hidden { get; init; }
    public string? AccentColor { get; init; }
    public BayIcon? Icon { get; init; }
}

public sealed record BayIcon(string Kind, string Value);

public sealed record Collection
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}

public sealed record RegistryModel
{
    private readonly int _schemaVersion = 1;
    public int SchemaVersion { get => _schemaVersion == 0 ? 1 : _schemaVersion; init => _schemaVersion = value; }
    private readonly IReadOnlyList<string>? _openBays;
    public IReadOnlyList<string> OpenBays { get => _openBays ?? []; init => _openBays = value; }
    private readonly IReadOnlyList<Collection>? _collections;
    public IReadOnlyList<Collection> Collections { get => _collections ?? []; init => _collections = value; }
    private readonly string? _activeCollectionId;
    public string ActiveCollectionId { get => _activeCollectionId ?? BayModel.DefaultCollectionId; init => _activeCollectionId = value; }
}

public sealed record BaySummary(string Id, string Name, string ProjectDir, string CollectionId, bool IsWorktree, bool Active, bool Hidden = false, string? IconKind = null, string? IconValue = null);

public sealed record BayCreateParams(string Name, string ProjectDir, string? CollectionId = null);

public sealed record BayIdParams(string Id);

public sealed record BayIdResult(string Id);

public sealed record BayListResult(IReadOnlyList<BaySummary> Bays);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BayModel))]
[JsonSerializable(typeof(RegistryModel))]
[JsonSerializable(typeof(BayIcon))]
[JsonSerializable(typeof(NookRecord))]
[JsonSerializable(typeof(Collection))]
[JsonSerializable(typeof(MosaicNode))]
[JsonSerializable(typeof(SplitNode))]
[JsonSerializable(typeof(NookLeaf))]
[JsonSerializable(typeof(BaySummary))]
[JsonSerializable(typeof(BayCreateParams))]
[JsonSerializable(typeof(BayIdParams))]
[JsonSerializable(typeof(BayIdResult))]
[JsonSerializable(typeof(BayListResult))]
public sealed partial class BaysJsonContext : JsonSerializerContext { }
