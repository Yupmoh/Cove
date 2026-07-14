using System.IO;
using Cove.Cli;
using Xunit;

namespace Cove.Cli.Tests;

public sealed class UsageTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    public void IsHelpRequested_True_For_Help_Tokens(string token)
    {
        Assert.True(CliUsage.IsHelpRequested(new[] { token }));
    }

    [Theory]
    [InlineData("exec")]
    [InlineData("frobnicate")]
    [InlineData("--json")]
    public void IsHelpRequested_False_For_Other_Tokens(string token)
    {
        Assert.False(CliUsage.IsHelpRequested(new[] { token }));
    }

    [Fact]
    public void Write_Renders_Usage_And_Catalogue_Without_Daemon()
    {
        var sw = new StringWriter();
        CliUsage.Write(sw);
        var text = sw.ToString();
        Assert.Contains("usage:", text);
        Assert.Contains("[cli] commands", text);
        Assert.Contains("[core]", text);
    }
}
