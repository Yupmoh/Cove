using Cove.Engine.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class FramingVariableResolverTests
{
    [Fact]
    public void Resolve_SingleVariable()
    {
        var resolver = new FramingVariableResolver(NullLogger.Instance);
        var result = resolver.Resolve("action-{actionId}", new Dictionary<string, string> { ["actionId"] = "abc123" });
        Assert.Equal("action-abc123", result);
    }

    [Fact]
    public void Resolve_MultipleVariables()
    {
        var resolver = new FramingVariableResolver(NullLogger.Instance);
        var result = resolver.Resolve("{actionId}-{nookId}-{bayId}", new Dictionary<string, string>
        {
            ["actionId"] = "act1",
            ["nookId"] = "nook1",
            ["bayId"] = "ws1",
        });
        Assert.Equal("act1-nook1-ws1", result);
    }

    [Fact]
    public void Resolve_NoVariables_ReturnsOriginal()
    {
        var resolver = new FramingVariableResolver(NullLogger.Instance);
        var result = resolver.Resolve("no variables here", new Dictionary<string, string> { ["actionId"] = "abc" });
        Assert.Equal("no variables here", result);
    }

    [Fact]
    public void Resolve_UnresolvedVariable_KeptAsIs()
    {
        var resolver = new FramingVariableResolver(NullLogger.Instance);
        var result = resolver.Resolve("{actionId}-{unknown}", new Dictionary<string, string> { ["actionId"] = "abc" });
        Assert.Equal("abc-{unknown}", result);
    }

    [Fact]
    public void Resolve_EmptyTemplate_ReturnsEmpty()
    {
        var resolver = new FramingVariableResolver(NullLogger.Instance);
        Assert.Equal("", resolver.Resolve("", new Dictionary<string, string> { ["actionId"] = "abc" }));
    }

    [Fact]
    public void Resolve_EmptyVariables_ReturnsOriginal()
    {
        var resolver = new FramingVariableResolver(NullLogger.Instance);
        Assert.Equal("test {actionId}", resolver.Resolve("test {actionId}", new Dictionary<string, string>()));
    }

    [Fact]
    public void ExtractVariableNames_FindsAll()
    {
        var resolver = new FramingVariableResolver(NullLogger.Instance);
        var names = resolver.ExtractVariableNames("{actionId}-{nookId}-{actionId}");
        Assert.Equal(2, names.Count);
        Assert.Contains("actionId", names);
        Assert.Contains("nookId", names);
    }

    [Fact]
    public void ExtractVariableNames_NoVariables_ReturnsEmpty()
    {
        var resolver = new FramingVariableResolver(NullLogger.Instance);
        Assert.Empty(resolver.ExtractVariableNames("no vars"));
    }

    [Fact]
    public void HasUnresolvedVariables_True_WhenUnresolved()
    {
        var resolver = new FramingVariableResolver(NullLogger.Instance);
        Assert.True(resolver.HasUnresolvedVariables("test {unknown}"));
    }

    [Fact]
    public void HasUnresolvedVariables_False_WhenAllResolved()
    {
        var resolver = new FramingVariableResolver(NullLogger.Instance);
        Assert.False(resolver.HasUnresolvedVariables("all resolved here"));
    }

    [Fact]
    public void Resolve_UrlWithActionId()
    {
        var resolver = new FramingVariableResolver(NullLogger.Instance);
        var result = resolver.Resolve("https://app.example.com/action/{actionId}", new Dictionary<string, string> { ["actionId"] = "xyz" });
        Assert.Equal("https://app.example.com/action/xyz", result);
    }

    [Fact]
    public void Resolve_AdjacentVariables()
    {
        var resolver = new FramingVariableResolver(NullLogger.Instance);
        var result = resolver.Resolve("{actionId}{nookId}", new Dictionary<string, string>
        {
            ["actionId"] = "a",
            ["nookId"] = "p",
        });
        Assert.Equal("ap", result);
    }
}
