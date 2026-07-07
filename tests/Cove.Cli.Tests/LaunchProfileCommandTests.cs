using Cove.Generated;
using Xunit;

namespace Cove.Cli.Tests;

public class LaunchProfileCommandTests
{
    [Theory]
    [InlineData("launch-profile list")]
    [InlineData("launch-profile set-default")]
    [InlineData("launch-profile delete")]
    public void LaunchProfileVerbs_AreRegistered(string verb)
    {
        Assert.Contains(verb, CoveCommandRegistry.Keys);
    }

    [Theory]
    [InlineData("launch-profile list")]
    [InlineData("launch-profile set-default")]
    [InlineData("launch-profile delete")]
    public void LaunchProfileVerbs_HaveHandlers(string verb)
    {
        Assert.True(CoveCommandRegistry.Handlers.ContainsKey(verb));
    }
}
