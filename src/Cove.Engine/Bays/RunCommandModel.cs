using System.Text.Json.Serialization;

namespace Cove.Engine.Bays;

public enum RunCommandLifecycle { NotLaunched, Running, Stopped }

public sealed record RunCommandDefinition
{
    public required string Id { get; init; }
    public required string BayId { get; init; }
    public required string Label { get; init; }
    public required string Command { get; init; }
    public string Cwd { get; init; } = "";
}

public sealed record RunCommandStatus
{
    public required string Id { get; init; }
    public required RunCommandLifecycle Lifecycle { get; init; }
    public required string SessionId { get; init; }
    public int? ExitCode { get; init; }
    public DateTimeOffset? StartedAtUtc { get; init; }
    public DateTimeOffset? StoppedAtUtc { get; init; }
}

public sealed record RunCommandListItem
{
    public required RunCommandDefinition Definition { get; init; }
    public required RunCommandLifecycle Lifecycle { get; init; }
    public bool Inherited { get; init; }
}

public interface IRunCommandStore
{
    Task<RunCommandDefinition?> GetAsync(string id);
    Task<IReadOnlyList<RunCommandDefinition>> ListAsync(string bayId);
    Task<RunCommandDefinition> SaveAsync(RunCommandDefinition def);
    Task<bool> DeleteAsync(string id);
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RunCommandDefinition))]
[JsonSerializable(typeof(RunCommandStatus))]
[JsonSerializable(typeof(RunCommandListItem))]
public sealed partial class RunCommandJsonContext : JsonSerializerContext { }
