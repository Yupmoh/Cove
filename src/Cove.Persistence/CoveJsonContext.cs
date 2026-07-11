using System.Text.Json.Serialization;

namespace Cove.Persistence;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CoveState))]
[JsonSerializable(typeof(WindowGeometry))]
[JsonSerializable(typeof(DataDirMeta))]
[JsonSerializable(typeof(BaySnapshot))]
[JsonSerializable(typeof(ShoreSnapshot))]
[JsonSerializable(typeof(MosaicNode))]
[JsonSerializable(typeof(SplitNode))]
[JsonSerializable(typeof(NookLeaf))]
[JsonSerializable(typeof(Subtab))]
[JsonSerializable(typeof(IReadOnlyList<Subtab>))]
[JsonSerializable(typeof(IReadOnlyList<ShoreSnapshot>))]
[JsonSerializable(typeof(NookDescriptor))]
[JsonSerializable(typeof(NookDescriptor[]))]
public sealed partial class CoveJsonContext : JsonSerializerContext { }
