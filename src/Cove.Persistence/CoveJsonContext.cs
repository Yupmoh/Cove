using System.Text.Json.Serialization;

namespace Cove.Persistence;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CoveState))]
[JsonSerializable(typeof(WindowGeometry))]
[JsonSerializable(typeof(DataDirMeta))]
public sealed partial class CoveJsonContext : JsonSerializerContext { }
