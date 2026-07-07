using Cove.Generated;
using Xunit;

namespace Cove.Cli.Tests;

public class ResolvePromptSigilsCommandTests
{
    [Fact]
    public void ResolvePromptSigils_IsRegisteredInGeneratedRegistry()
    {
        Assert.Contains("skills resolve-prompt-sigils", CoveCommandRegistry.Keys);
    }

    [Fact]
    public void ResolvePromptSigils_HasHandlerInGeneratedRegistry()
    {
        Assert.True(CoveCommandRegistry.Handlers.ContainsKey("skills resolve-prompt-sigils"));
    }
}
