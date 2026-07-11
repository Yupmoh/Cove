using Cove.Engine.Nooks;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ObservatoryExclusionTests
{
    [Fact]
    public void Observatory_IsNotRegistered_AsNookType()
    {
        var registry = NookTypeRegistry.CreateWithBuiltins();
        var types = registry.List();
        Assert.DoesNotContain(types, t => t.Name == "observatory");
    }

    [Fact]
    public void Observatory_IsNotCreatable_ViaNookCreate()
    {
        var registry = NookTypeRegistry.CreateWithBuiltins();
        Assert.False(registry.IsRegistered("observatory"));
    }

    [Fact]
    public void Browser_IsRegistered_AsNookType()
    {
        var registry = NookTypeRegistry.CreateWithBuiltins();
        Assert.True(registry.IsRegistered("browser"));
    }

    [Fact]
    public void Terminal_IsRegistered_AsNookType()
    {
        var registry = NookTypeRegistry.CreateWithBuiltins();
        Assert.True(registry.IsRegistered("terminal"));
    }
}
