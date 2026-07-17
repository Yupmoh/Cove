using Cove.Engine.Bays;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class WorktreePatternTests
{
    [Fact]
    public void Expand_BothTokens_Replaced()
    {
        var result = WorktreePattern.Expand("../{repo}-worktrees/{branch}", "myrepo", "feature-x");
        Assert.Equal("../myrepo-worktrees/feature-x", result);
    }

    [Fact]
    public void Expand_BothTokens_MultipleOccurrencesReplaced()
    {
        var result = WorktreePattern.Expand("{repo}/{branch}/{repo}", "app", "dev");
        Assert.Equal("app/dev/app", result);
    }

    [Fact]
    public void Expand_MissingTokens_LeftLiteral()
    {
        var result = WorktreePattern.Expand("../fixed-path/branch", "repo", "main");
        Assert.Equal("../fixed-path/branch", result);
    }

    [Fact]
    public void Expand_PartialTokens_OnlyPresentReplaced()
    {
        var result = WorktreePattern.Expand("{repo}-wt", "myrepo", "ignored");
        Assert.Equal("myrepo-wt", result);
    }

    [Fact]
    public void Expand_EmptyPattern_ReturnsEmpty()
    {
        Assert.Equal("", WorktreePattern.Expand("", "repo", "branch"));
    }

    [Fact]
    public void Expand_DefaultPattern_MatchesExpectedShape()
    {
        var result = WorktreePattern.Expand("../{repo}-worktrees/{branch}", "cove", "feature");
        Assert.Equal("../cove-worktrees/feature", result);
    }

    [Fact]
    public void DeriveRepoName_FromProjectDir()
    {
        Assert.Equal("myrepo", WorktreePattern.DeriveRepoName("/home/user/projects/myrepo"));
    }

    [Fact]
    public void DeriveRepoName_TrailingSlashStripped()
    {
        Assert.Equal("myrepo", WorktreePattern.DeriveRepoName("/home/user/projects/myrepo/"));
    }

    [Fact]
    public void DeriveRepoName_EmptyPath_ReturnsDefault()
    {
        Assert.Equal("repo", WorktreePattern.DeriveRepoName(""));
    }

    [Fact]
    public void DeriveRepoName_RootPath_ReturnsDefault()
    {
        Assert.Equal("repo", WorktreePattern.DeriveRepoName("/"));
    }

    [Fact]
    public void ResolveLocation_RelativeResolvedAgainstParentProjectDir()
    {
        var root = System.IO.Path.GetTempPath();
        var parent = System.IO.Path.Combine(root, "projects", "cove");
        var result = WorktreePattern.ResolveLocation(System.IO.Path.Combine("..", "cove-worktrees", "feature"), parent);
        Assert.Equal(System.IO.Path.GetFullPath(System.IO.Path.Combine(root, "projects", "cove-worktrees", "feature")), result);
    }

    [Fact]
    public void ResolveLocation_AbsoluteUnchanged()
    {
        var abs = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wt");
        var parent = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove");
        Assert.Equal(System.IO.Path.GetFullPath(abs), WorktreePattern.ResolveLocation(abs, parent));
    }

    [Fact]
    public void ResolveLocation_DefaultPatternExpansionYieldsSiblingDir()
    {
        var root = System.IO.Path.GetTempPath();
        var parent = System.IO.Path.Combine(root, "repos", "cove");
        var expanded = WorktreePattern.Expand("../{repo}-worktrees/{branch}", "cove", "fix-1");
        var result = WorktreePattern.ResolveLocation(expanded, parent);
        Assert.Equal(System.IO.Path.GetFullPath(System.IO.Path.Combine(root, "repos", "cove-worktrees", "fix-1")), result);
    }
}
