using Cove.Adapters;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class AdapterCommandsTests
{
    [Fact]
    public void Resolve_NoOverride_ReturnsOriginalCommand()
    {
        var overrides = new AdapterCommandOverrides();
        var cmd = overrides.Resolve("claude-code", new[] { "claude", "--resume", "abc" });
        Assert.Equal(new[] { "claude", "--resume", "abc" }, cmd);
    }

    [Fact]
    public void Resolve_WithOverride_WrapsCommand()
    {
        var overrides = new AdapterCommandOverrides
        {
            { "claude-code", new[] { "env", "CLAUDE_CODE=1" } }
        };
        var cmd = overrides.Resolve("claude-code", new[] { "claude", "--resume", "abc" });
        Assert.Equal(new[] { "env", "CLAUDE_CODE=1", "claude", "--resume", "abc" }, cmd);
    }

    [Fact]
    public void Resolve_DifferentAdapter_NotAffected()
    {
        var overrides = new AdapterCommandOverrides
        {
            { "claude-code", new[] { "wrapper" } }
        };
        var cmd = overrides.Resolve("codex", new[] { "codex" });
        Assert.Equal(new[] { "codex" }, cmd);
    }

    [Fact]
    public void Resolve_EmptyOverride_PassesThrough()
    {
        var overrides = new AdapterCommandOverrides
        {
            { "claude-code", Array.Empty<string>() }
        };
        var cmd = overrides.Resolve("claude-code", new[] { "claude" });
        Assert.Equal(new[] { "claude" }, cmd);
    }
}
