using System.Text.Json.Serialization;

namespace Cove.Engine.Launch;

public sealed record LauncherOptionChoice(string Value, string? Label);

public sealed record LauncherOption(
    string Key,
    string Label,
    string Type,
    string? DefaultValueRaw,
    IReadOnlyList<LauncherOptionChoice>? Choices);

public sealed record LauncherSuggestedFlag(string Flag, string? Description, IReadOnlyList<string>? Values);

public sealed record LauncherOptionsResult(IReadOnlyList<LauncherOption> Options, IReadOnlyList<LauncherSuggestedFlag> SuggestedFlags);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LauncherOptionsResult))]
[JsonSerializable(typeof(LauncherSuggestedFlag))]
public sealed partial class LauncherOptionsResultJsonContext : JsonSerializerContext { }
