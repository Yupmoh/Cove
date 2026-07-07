using Cove.Generated;
using Xunit;

namespace Cove.Cli.Tests;

public class SkillsCommandTests
{
    [Fact]
    public void SkillsList_IsRegisteredInGeneratedRegistry()
    {
        Assert.Contains("skills list", CoveCommandRegistry.Keys);
    }

    [Fact]
    public void SkillsList_HasHandlerInGeneratedRegistry()
    {
        Assert.True(CoveCommandRegistry.Handlers.ContainsKey("skills list"));
    }
}
