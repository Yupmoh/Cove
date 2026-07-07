using System.Text.Json.Serialization;

namespace Cove.Engine.Launch;

public sealed record LauncherOptionChoice(string Value, string? Label);

public sealed record LauncherOption(
    string Key,
    string Label,
    string Type,
    string? DefaultValueRaw,
    IReadOnlyList<LauncherOptionChoice>? Choices);

public sealed record LauncherOptionsResult(IReadOnlyList<LauncherOption> Options);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LauncherOptionsResult))]
public sealed partial class LauncherOptionsResultJsonContext : JsonSerializerContext { }
