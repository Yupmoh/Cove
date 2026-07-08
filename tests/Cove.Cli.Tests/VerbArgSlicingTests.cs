using Cove.Cli;
using Xunit;

namespace Cove.Cli.Tests;

public sealed class VerbArgSlicingTests
{
    [Fact]
    public void Slice_AttachRaw_PreservesRawFlagAndSession()
    {
        var args = CommandContext.SliceVerbArgs("attach", new[] { "attach", "--raw", "session-123" });
        Assert.Equal(new[] { "--raw", "session-123" }, args);
    }

    [Fact]
    public void Slice_ConfigSet_PreservesKeyAndValue()
    {
        var args = CommandContext.SliceVerbArgs("config set", new[] { "config", "set", "theme", "dracula" });
        Assert.Equal(new[] { "theme", "dracula" }, args);
    }

    [Fact]
    public void Slice_ExtensionRun_PreservesParamsFlag()
    {
        var args = CommandContext.SliceVerbArgs("extension run", new[] { "extension", "run", "ext.adapter.method", "--params", "{\"k\":1}" });
        Assert.Equal(new[] { "ext.adapter.method", "--params", "{\"k\":1}" }, args);
    }

    [Fact]
    public void Slice_StripsGlobalFlags()
    {
        var args = CommandContext.SliceVerbArgs("config set", new[] { "config", "set", "--json", "theme", "dracula" });
        Assert.Equal(new[] { "theme", "dracula" }, args);
    }

    [Fact]
    public void Slice_StripsChannelWithValue()
    {
        var args = CommandContext.SliceVerbArgs("config set", new[] { "--channel", "beta", "config", "set", "theme", "dracula" });
        Assert.Equal(new[] { "theme", "dracula" }, args);
    }

    [Fact]
    public void Slice_DocsGenerate_GetsOutputPath()
    {
        var args = CommandContext.SliceVerbArgs("docs generate", new[] { "docs", "generate", "custom.md" });
        Assert.Equal(new[] { "custom.md" }, args);
    }

    [Fact]
    public void Slice_NoExtraArgs_ReturnsEmpty()
    {
        var args = CommandContext.SliceVerbArgs("commands", new[] { "commands" });
        Assert.Empty(args);
    }
}
