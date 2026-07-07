using Cove.Engine.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class PrefixResolverTests
{
    [Fact]
    public void Resolve_UniquePrefix_ReturnsFullId()
    {
        var resolver = new PrefixResolver();
        resolver.Index("pane", "pane-abc123");
        resolver.Index("pane", "pane-def456");

        var result = resolver.Resolve("pane", "pane-a");

        Assert.True(result.Found);
        Assert.Equal("pane-abc123", result.Id);
    }

    [Fact]
    public void Resolve_AmbiguousPrefix_ReturnsAmbiguous()
    {
        var resolver = new PrefixResolver();
        resolver.Index("pane", "pane-abc123");
        resolver.Index("pane", "pane-abc999");

        var result = resolver.Resolve("pane", "pane-abc");

        Assert.False(result.Found);
        Assert.Equal("ambiguous_id", result.ErrorCode);
    }

    [Fact]
    public void Resolve_NoMatch_ReturnsNotFound()
    {
        var resolver = new PrefixResolver();
        resolver.Index("pane", "pane-abc123");

        var result = resolver.Resolve("pane", "never-seen");

        Assert.False(result.Found);
        Assert.Equal("not_found", result.ErrorCode);
    }

    [Fact]
    public void Resolve_EmptyPrefix_ReturnsNotFound()
    {
        var resolver = new PrefixResolver();
        resolver.Index("pane", "pane-abc123");

        var result = resolver.Resolve("pane", "");

        Assert.False(result.Found);
        Assert.Equal("not_found", result.ErrorCode);
    }

    [Fact]
    public void Resolve_FullId_ReturnsExact()
    {
        var resolver = new PrefixResolver();
        resolver.Index("pane", "pane-abc123");
        resolver.Index("pane", "pane-abc124");

        var result = resolver.Resolve("pane", "pane-abc123");

        Assert.True(result.Found);
        Assert.Equal("pane-abc123", result.Id);
    }

    [Fact]
    public void Resolve_DifferentTypesAreIndependent()
    {
        var resolver = new PrefixResolver();
        resolver.Index("pane", "abc123");
        resolver.Index("task", "abc123");

        var paneResult = resolver.Resolve("pane", "abc");
        var taskResult = resolver.Resolve("task", "abc");

        Assert.True(paneResult.Found);
        Assert.Equal("abc123", paneResult.Id);
        Assert.True(taskResult.Found);
        Assert.Equal("abc123", taskResult.Id);
    }

    [Fact]
    public void Resolve_RejectsPrefix_WhenFlagged()
    {
        var resolver = new PrefixResolver();
        resolver.Index("pane", "pane-abc123");

        var result = resolver.Resolve("pane", "pane-a", rejectPrefix: true);

        Assert.False(result.Found);
        Assert.Equal("not_found", result.ErrorCode);
    }
}
