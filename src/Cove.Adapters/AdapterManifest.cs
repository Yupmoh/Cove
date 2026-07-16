using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Protocol;
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
    public string? Author { get; init; }
    public required IReadOnlyDictionary<string, AdapterMethod> Methods { get; init; }
    public BinaryDiscovery? BinaryDiscovery { get; init; }
    public string? Icon { get; init; }
    public string? SkillInstallPath { get; init; }
    public IReadOnlyList<string> WellKnownPaths { get; init; } = [];
    public IReadOnlyList<string> SuggestedFlags { get; init; } = [];
    public AdapterRetention? Retention { get; init; }
    public SessionExtractor? SessionExtractor { get; init; }
    public LauncherOptions? LauncherOptions { get; init; }
    public ScreenStateDeclaration? ScreenState { get; init; }
    public IReadOnlyDictionary<string, string> Hooks { get; init; } = new Dictionary<string, string>();
    public string? SkillsDir { get; init; }
    private readonly IReadOnlyDictionary<string, InstallRecipe>? _install;
    public IReadOnlyDictionary<string, InstallRecipe> Install { get => _install ?? new Dictionary<string, InstallRecipe>(); init => _install = value; }
    private readonly IReadOnlyDictionary<string, InstallRecipe>? _update;
    public IReadOnlyDictionary<string, InstallRecipe> Update { get => _update ?? new Dictionary<string, InstallRecipe>(); init => _update = value; }
    private readonly IReadOnlyDictionary<string, InstallRecipe>? _uninstall;
    public IReadOnlyDictionary<string, InstallRecipe> Uninstall { get => _uninstall ?? new Dictionary<string, InstallRecipe>(); init => _uninstall = value; }
    public IReadOnlyDictionary<string, HookEnvelopeDeclaration> HookEnvelopes { get; init; } = new Dictionary<string, HookEnvelopeDeclaration>();
}

public sealed record HookEnvelopeDeclaration
{
    public required HookEnvelopeKind Kind { get; init; }
    public bool IncludeSystemMessage { get; init; }
}

public sealed record ScreenStateRule
{
    public required string Pattern { get; init; }
    public required string Status { get; init; }
}

public sealed record ScreenStateDeclaration
{
    private static readonly HashSet<string> Vocabulary = new()
    {
        "idle", "active", "tool-running", "needs-input", "needs-permission", "done", "error",
    };

    public int QuietMs { get; init; } = 2000;
    public int TailBytes { get; init; } = 4096;
    public IReadOnlyList<ScreenStateRule> Rules { get; init; } = [];

    [JsonIgnore]
    public int EffectiveTailBytes => Math.Clamp(TailBytes, 256, 65536);

    [JsonIgnore]
    public int EffectiveQuietMs => Math.Max(250, QuietMs);

    public static bool IsValidStatus(string status) => Vocabulary.Contains(status);
}

[JsonConverter(typeof(HookEnvelopeKindConverter))]
public enum HookEnvelopeKind { None, Identity, HookSpecificOutput, FlatAdditionalContext }

sealed class HookEnvelopeKindConverter : JsonStringEnumConverter<HookEnvelopeKind>
{
    public HookEnvelopeKindConverter() : base(JsonNamingPolicy.CamelCase, allowIntegerValues: false) { }
}

public sealed record AdapterMethod
{
    public string? Script { get; init; }
    public string? Static { get; init; }
}

public sealed record BinaryDiscovery
{
    private readonly IReadOnlyList<string>? _commands;
    public IReadOnlyList<string> Commands { get => _commands ?? []; init => _commands = value; }
    private readonly IReadOnlyList<string>? _wellKnownPaths;
    public IReadOnlyList<string> WellKnownPaths { get => _wellKnownPaths ?? []; init => _wellKnownPaths = value; }
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
    private readonly int _schemaVersion = 1;
    public int SchemaVersion { get => _schemaVersion == 0 ? 1 : _schemaVersion; init => _schemaVersion = value; }
}

public sealed record LauncherOptions
{
    private readonly IReadOnlyDictionary<string, string>? _static;
    public IReadOnlyDictionary<string, string> Static { get => _static ?? new Dictionary<string, string>(); init => _static = value; }
}

public sealed record RegistryEntry
{
    private readonly int _schemaVersion = 1;
    public int SchemaVersion { get => _schemaVersion == 0 ? 1 : _schemaVersion; init => _schemaVersion = value; }
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
    private readonly IReadOnlyList<string>? _models;
    public IReadOnlyList<string> Models { get => _models ?? []; init => _models = value; }
    private readonly IReadOnlyList<string>? _platforms;
    public IReadOnlyList<string> Platforms { get => _platforms ?? []; init => _platforms = value; }
    private readonly IReadOnlyDictionary<string, InstallRecipe>? _install;
    public IReadOnlyDictionary<string, InstallRecipe> Install { get => _install ?? new Dictionary<string, InstallRecipe>(); init => _install = value; }
}

public sealed record InstallRecipe
{
    public required string Cmd { get; init; }
    public bool? PostInstallAuth { get; init; }
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
    public string? NookId { get; init; }
    public string? SessionId { get; init; }
    public System.Text.Json.JsonElement? Payload { get; init; }
}

public sealed record NookSelection(string Slug, DateTimeOffset LastUsedAt);

public sealed record NookSelectionStore(
    IReadOnlyDictionary<string, NookSelection> NookSelections,
    string? LastUsed);

public sealed record FooterChipData(string ProfileSlug, bool IsDefault, DateTimeOffset? LastUsedAt);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    AllowOutOfOrderMetadataProperties = true)]
[JsonSerializable(typeof(AdapterManifest))]
[JsonSerializable(typeof(AdapterMethod))]
[JsonSerializable(typeof(BinaryDiscovery))]
[JsonSerializable(typeof(HookEnvelopeDeclaration))]
[JsonSerializable(typeof(Dictionary<string, HookEnvelopeDeclaration>))]
[JsonSerializable(typeof(ScreenStateDeclaration))]
[JsonSerializable(typeof(ScreenStateRule))]
[JsonSerializable(typeof(List<AdapterEnvVar>))]
[JsonSerializable(typeof(Registry))]
[JsonSerializable(typeof(List<RegistryEntry>))]
[JsonSerializable(typeof(InstallRecipe))]
[JsonSerializable(typeof(InstalledAdapter))]
[JsonSerializable(typeof(HookEvent))]
[JsonSerializable(typeof(RecentSession))]
[JsonSerializable(typeof(List<RecentSession>))]
[JsonSerializable(typeof(LaunchProfile))]
[JsonSerializable(typeof(NookSelectionStore))]
[JsonSerializable(typeof(NookSelection))]
[JsonSerializable(typeof(FooterChipData))]
public sealed partial class AdaptersJsonContext : JsonSerializerContext { }
