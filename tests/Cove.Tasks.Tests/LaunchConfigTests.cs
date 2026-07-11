using Cove.Tasks.LaunchConfig;
using Xunit;

namespace Cove.Tasks.Tests;

public sealed class LaunchConfigTests
{
    private static LaunchConfigModel ValidConfig() => new()
    {
        Adapter = "claude",
        ProfileSlug = "default",
        ExecutionMode = "nook",
        InProgressStatusId = "in-progress",
        ReviewStatusId = "in-review",
        CompletionStatusId = "done",
        MergeTarget = "main",
        WorktreeBranchSource = "task",
        WorktreeBranchName = "COVE-1",
    };

    private static LaunchConfigValidationContext ValidContext() => new(
        KnownAdapters: new System.Collections.Generic.HashSet<string> { "claude", "codex" },
        KnownStatuses: new System.Collections.Generic.HashSet<string> { "todo", "in-progress", "in-review", "done", "looping" },
        KnownProfileSlugs: new System.Collections.Generic.HashSet<string> { "default" });

    [Fact]
    public void Serialize_RoundTripsAllFields()
    {
        var config = ValidConfig();
        var json = LaunchConfigSerializer.Serialize(config);
        var parsed = LaunchConfigSerializer.Deserialize(json);
        Assert.NotNull(parsed);
        Assert.Equal("claude", parsed!.Adapter);
        Assert.Equal("default", parsed.ProfileSlug);
        Assert.Equal("nook", parsed.ExecutionMode);
        Assert.Equal("in-progress", parsed.InProgressStatusId);
        Assert.Equal("in-review", parsed.ReviewStatusId);
        Assert.Equal("done", parsed.CompletionStatusId);
        Assert.Equal("main", parsed.MergeTarget);
        Assert.Equal("task", parsed.WorktreeBranchSource);
        Assert.Equal("COVE-1", parsed.WorktreeBranchName);
    }

    [Fact]
    public void Validate_ValidConfig_ReturnsNoErrors()
    {
        var result = LaunchConfigValidator.Validate(ValidConfig(), ValidContext());
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_UnknownAdapter_ReturnsError()
    {
        var config = ValidConfig() with { Adapter = "nonexistent" };
        var result = LaunchConfigValidator.Validate(config, ValidContext());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("adapter") && e.Contains("nonexistent"));
    }

    [Fact]
    public void Validate_UnknownStatusGate_ReturnsError()
    {
        var config = ValidConfig() with { InProgressStatusId = "nonexistent" };
        var result = LaunchConfigValidator.Validate(config, ValidContext());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("in_progress_status") || e.Contains("nonexistent"));
    }

    [Fact]
    public void Validate_WorktreeModeWithoutBranchSource_ReturnsError()
    {
        var config = ValidConfig() with { ExecutionMode = "worktree", WorktreeBranchSource = null };
        var result = LaunchConfigValidator.Validate(config, ValidContext());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("branch_source") || e.Contains("worktree"));
    }

    [Fact]
    public void Validate_NookModeIgnoresWorktreeFields()
    {
        var config = ValidConfig() with { ExecutionMode = "nook", WorktreeBranchSource = null, MergeTarget = null };
        var result = LaunchConfigValidator.Validate(config, ValidContext());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_UnknownProfileSlug_ReturnsError()
    {
        var config = ValidConfig() with { ProfileSlug = "nonexistent" };
        var result = LaunchConfigValidator.Validate(config, ValidContext());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("profile") && e.Contains("nonexistent"));
    }

    [Fact]
    public void Deserialize_NullJson_ReturnsNull()
    {
        var parsed = LaunchConfigSerializer.Deserialize(null);
        Assert.Null(parsed);
    }

    [Fact]
    public void Deserialize_EmptyJson_ReturnsNull()
    {
        var parsed = LaunchConfigSerializer.Deserialize("");
        Assert.Null(parsed);
    }

    [Fact]
    public void Deserialize_MalformedJson_ReturnsNull()
    {
        var parsed = LaunchConfigSerializer.Deserialize("{not valid json");
        Assert.Null(parsed);
    }
}
