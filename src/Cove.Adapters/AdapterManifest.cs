using System.Text.Json.Serialization;

namespace Cove.Adapters;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdapterManifest
{
    public int SdkVersion { get; init; }
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required string Accent { get; init; }
    public required string Binary { get; init; }
    public required string Version { get; init; }
    public required IReadOnlyDictionary<string, AdapterMethod> Methods { get; init; }
    public BinaryDiscovery? BinaryDiscovery { get; init; }
    public string? IconSvg { get; init; }
    public string? SkillInstallPath { get; init; }
    public IReadOnlyList<string> WellKnownPaths { get; init; } = [];
    public IReadOnlyList<string> SuggestedFlags { get; init; } = [];
    public AdapterRetention? Retention { get; init; }
    public SessionExtractor? SessionExtractor { get; init; }
    public LauncherOptions? LauncherOptions { get; init; }
}

public sealed record AdapterMethod
{
    public string? Script { get; init; }
    public string? Static { get; init; }
}

public sealed record BinaryDiscovery
{
    public IReadOnlyList<string> Commands { get; init; } = [];
    public string? VersionFlag { get; init; }
    public string? VersionRegex { get; init; }
}

public sealed record AdapterRetention
{
    public string? ReadScript { get; init; }
    public string? WriteScript { get; init; }
    public string? Recommended { get; init; }
}

public sealed record SessionExtractor
{
    public required string Script { get; init; }
    public int SchemaVersion { get; init; } = 1;
}

public sealed record LauncherOptions
{
    public IReadOnlyDictionary<string, string> Static { get; init; } = new Dictionary<string, string>();
}

public sealed record RegistryEntry
{
    public int SchemaVersion { get; init; } = 1;
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required string Accent { get; init; }
    public required string Binary { get; init; }
    public int SdkVersion { get; init; }
    public required string Version { get; init; }
    public string? MinAppVersion { get; init; }
    public bool Official { get; init; }
    public string? IconSvg { get; init; }
    public IReadOnlyList<string> Models { get; init; } = [];
    public IReadOnlyDictionary<string, InstallRecipe> Install { get; init; } = new Dictionary<string, InstallRecipe>();
}

public sealed record InstallRecipe
{
    public required string Cmd { get; init; }
    public string? PostInstallAuth { get; init; }
}

public sealed record InstalledAdapter
{
    public required string Name { get; init; }
    public required string Dir { get; init; }
    public required AdapterManifest Manifest { get; init; }
    public string? BinaryPath { get; init; }
    public string? Version { get; init; }
    public AdapterDetectionState DetectionState { get; init; }
}

public enum AdapterDetectionState { Detected, Broken, Missing }

public sealed record HookEvent
{
    public required string Adapter { get; init; }
    public required string Event { get; init; }
    public string? PaneId { get; init; }
    public string? SessionId { get; init; }
    public System.Text.Json.JsonElement? Payload { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    AllowOutOfOrderMetadataProperties = true)]
[JsonSerializable(typeof(AdapterManifest))]
[JsonSerializable(typeof(AdapterMethod))]
[JsonSerializable(typeof(BinaryDiscovery))]
[JsonSerializable(typeof(AdapterRetention))]
[JsonSerializable(typeof(SessionExtractor))]
[JsonSerializable(typeof(Registry))]
[JsonSerializable(typeof(List<RegistryEntry>))]
[JsonSerializable(typeof(InstallRecipe))]
[JsonSerializable(typeof(InstalledAdapter))]
[JsonSerializable(typeof(HookEvent))]
[JsonSerializable(typeof(RecentSession))]
[JsonSerializable(typeof(List<RecentSession>))]
[JsonSerializable(typeof(CanonicalEvent))]
public sealed partial class AdaptersJsonContext : JsonSerializerContext { }
