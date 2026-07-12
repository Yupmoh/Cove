using System;
using System.Collections.Generic;
using Cove.Platform.Pty.Windows;
using Xunit;

namespace Cove.Platform.Tests;

public sealed class WindowsEnvironmentBlockTests
{
    [Fact]
    public void BuildEntries_OverlaysOverridesOverBaseCaseInsensitively()
    {
        var baseEnv = new Dictionary<string, string> { ["PATH"] = "x", ["FOO"] = "bar" };
        var overrides = new Dictionary<string, string> { ["foo"] = "baz", ["COVE_NOOK_ID"] = "n1" };

        var entries = WindowsEnvironmentBlock.BuildEntries(baseEnv, overrides);

        Assert.Equal(new[] { "COVE_NOOK_ID=n1", "FOO=baz", "PATH=x" }, entries);
    }

    [Fact]
    public void BuildEntries_SortsKeysCaseInsensitively()
    {
        var baseEnv = new Dictionary<string, string> { ["Zeta"] = "1", ["alpha"] = "2", ["Beta"] = "3" };

        var entries = WindowsEnvironmentBlock.BuildEntries(baseEnv, null);

        Assert.Equal(new[] { "alpha=2", "Beta=3", "Zeta=1" }, entries);
    }

    [Fact]
    public void BuildEntries_DoesNotInjectTerm()
    {
        var baseEnv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var entries = WindowsEnvironmentBlock.BuildEntries(baseEnv, null);

        Assert.Empty(entries);
        foreach (var entry in WindowsEnvironmentBlock.BuildEntries(
                     new Dictionary<string, string> { ["FOO"] = "bar" }, null))
            Assert.DoesNotContain("TERM=", entry, StringComparison.Ordinal);
    }

    [Fact]
    public void ToNullDelimitedBlock_IsDoubleNullTerminated()
    {
        var block = WindowsEnvironmentBlock.ToNullDelimitedBlock(new[] { "A=1", "B=2" });

        Assert.Equal("A=1\0B=2\0\0".ToCharArray(), block);
    }

    [Fact]
    public void ToNullDelimitedBlock_EmptyEntries_IsSingleNullBlock()
    {
        var block = WindowsEnvironmentBlock.ToNullDelimitedBlock(Array.Empty<string>());

        Assert.Equal("\0".ToCharArray(), block);
    }
}
