using Cove.Generated;
using Xunit;

namespace Cove.Cli.Tests;

public class AdapterEnvCommandTests
{
    [Theory]
    [InlineData("adapter-env list")]
    [InlineData("adapter-env save")]
    [InlineData("adapter-env resolve")]
    public void AdapterEnvVerbs_AreRegistered(string verb)
    {
        Assert.Contains(verb, CoveCommandRegistry.Keys);
    }

    [Theory]
    [InlineData("adapter-env list")]
    [InlineData("adapter-env save")]
    [InlineData("adapter-env resolve")]
    public void AdapterEnvVerbs_HaveHandlers(string verb)
    {
        Assert.True(CoveCommandRegistry.Handlers.ContainsKey(verb));
    }
}
