using Cove.Engine.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class PrefixResolverTests
{
    [Fact]
    public void Resolve_UniquePrefix_ReturnsFullId()
    {
        var resolver = new PrefixResolver();
        resolver.Index("nook", "nook-abc123");
        resolver.Index("nook", "nook-def456");

        var result = resolver.Resolve("nook", "nook-a");

        Assert.True(result.Found);
        Assert.Equal("nook-abc123", result.Id);
    }

    [Fact]
    public void Resolve_AmbiguousPrefix_ReturnsAmbiguous()
    {
        var resolver = new PrefixResolver();
        resolver.Index("nook", "nook-abc123");
        resolver.Index("nook", "nook-abc999");

        var result = resolver.Resolve("nook", "nook-abc");

        Assert.False(result.Found);
        Assert.Equal("ambiguous_id", result.ErrorCode);
    }

    [Fact]
    public void Resolve_NoMatch_ReturnsNotFound()
    {
        var resolver = new PrefixResolver();
        resolver.Index("nook", "nook-abc123");

        var result = resolver.Resolve("nook", "never-seen");

        Assert.False(result.Found);
        Assert.Equal("not_found", result.ErrorCode);
    }

    [Fact]
    public void Resolve_EmptyPrefix_ReturnsNotFound()
    {
        var resolver = new PrefixResolver();
        resolver.Index("nook", "nook-abc123");

        var result = resolver.Resolve("nook", "");

        Assert.False(result.Found);
        Assert.Equal("not_found", result.ErrorCode);
    }

    [Fact]
    public void Resolve_FullId_ReturnsExact()
    {
        var resolver = new PrefixResolver();
        resolver.Index("nook", "nook-abc123");
        resolver.Index("nook", "nook-abc124");

        var result = resolver.Resolve("nook", "nook-abc123");

        Assert.True(result.Found);
        Assert.Equal("nook-abc123", result.Id);
    }

    [Fact]
    public void Resolve_DifferentTypesAreIndependent()
    {
        var resolver = new PrefixResolver();
        resolver.Index("nook", "abc123");
        resolver.Index("task", "abc123");

        var nookResult = resolver.Resolve("nook", "abc");
        var taskResult = resolver.Resolve("task", "abc");

        Assert.True(nookResult.Found);
        Assert.Equal("abc123", nookResult.Id);
        Assert.True(taskResult.Found);
        Assert.Equal("abc123", taskResult.Id);
    }

    [Fact]
    public void Resolve_RejectsPrefix_WhenFlagged()
    {
        var resolver = new PrefixResolver();
        resolver.Index("nook", "nook-abc123");

        var result = resolver.Resolve("nook", "nook-a", rejectPrefix: true);

        Assert.False(result.Found);
        Assert.Equal("not_found", result.ErrorCode);
    }
}
