using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cove.Tasks.LaunchConfig;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(LaunchConfigModel))]
public sealed partial class TaskJsonContext : JsonSerializerContext;
