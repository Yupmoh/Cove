namespace Cove.Tasks.LaunchConfig;

public sealed record LaunchConfigModel
{
    public string? Adapter { get; init; }
    public string? ProfileSlug { get; init; }
    public string ExecutionMode { get; init; } = "pane";
    public string? InProgressStatusId { get; init; }
    public string? ReviewStatusId { get; init; }
    public string? CompletionStatusId { get; init; }
    public string? MergeTarget { get; init; }
    public string? WorktreeBranchSource { get; init; }
    public string? WorktreeBranchName { get; init; }
}

public sealed record LaunchConfigValidationContext(
    System.Collections.Generic.IReadOnlySet<string> KnownAdapters,
    System.Collections.Generic.IReadOnlySet<string> KnownStatuses,
    System.Collections.Generic.IReadOnlySet<string> KnownProfileSlugs);

public sealed record LaunchConfigValidationResult(bool IsValid, System.Collections.Generic.IReadOnlyList<string> Errors);
