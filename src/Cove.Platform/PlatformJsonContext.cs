using System.Text.Json.Serialization;

namespace Cove.Platform;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DataDirMeta))]
public sealed partial class PlatformJsonContext : JsonSerializerContext;
