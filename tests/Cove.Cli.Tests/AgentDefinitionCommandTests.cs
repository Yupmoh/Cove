using Cove.Generated;
using Xunit;

namespace Cove.Cli.Tests;

public class AgentDefinitionCommandTests
{
    [Theory]
    [InlineData("agent definition list")]
    [InlineData("agent definition show")]
    [InlineData("agent definition delete")]
    public void AgentDefinitionVerbs_AreRegistered(string verb)
    {
        Assert.Contains(verb, CoveCommandRegistry.Keys);
    }

    [Theory]
    [InlineData("agent definition list")]
    [InlineData("agent definition show")]
    [InlineData("agent definition delete")]
    public void AgentDefinitionVerbs_HaveHandlers(string verb)
    {
        Assert.True(CoveCommandRegistry.Handlers.ContainsKey(verb));
    }
}
