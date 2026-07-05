using System.Text.Json.Serialization;

namespace Cove.Persistence;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CoveState))]
[JsonSerializable(typeof(WindowGeometry))]
[JsonSerializable(typeof(DataDirMeta))]
[JsonSerializable(typeof(WorkspaceSnapshot))]
[JsonSerializable(typeof(RoomSnapshot))]
[JsonSerializable(typeof(MosaicNode))]
[JsonSerializable(typeof(SplitNode))]
[JsonSerializable(typeof(PaneLeaf))]
[JsonSerializable(typeof(Subtab))]
[JsonSerializable(typeof(IReadOnlyList<Subtab>))]
[JsonSerializable(typeof(IReadOnlyList<RoomSnapshot>))]
[JsonSerializable(typeof(PaneDescriptor))]
[JsonSerializable(typeof(PaneDescriptor[]))]
public sealed partial class CoveJsonContext : JsonSerializerContext { }
