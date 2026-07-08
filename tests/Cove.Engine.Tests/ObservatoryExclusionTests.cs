using Cove.Engine.Panes;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ObservatoryExclusionTests
{
    [Fact]
    public void Observatory_IsNotRegistered_AsPaneType()
    {
        var registry = PaneTypeRegistry.CreateWithBuiltins();
        var types = registry.List();
        Assert.DoesNotContain(types, t => t.Name == "observatory");
    }

    [Fact]
    public void Observatory_IsNotCreatable_ViaPaneCreate()
    {
        var registry = PaneTypeRegistry.CreateWithBuiltins();
        Assert.False(registry.IsRegistered("observatory"));
    }

    [Fact]
    public void Browser_IsRegistered_AsPaneType()
    {
        var registry = PaneTypeRegistry.CreateWithBuiltins();
        Assert.True(registry.IsRegistered("browser"));
    }

    [Fact]
    public void Terminal_IsRegistered_AsPaneType()
    {
        var registry = PaneTypeRegistry.CreateWithBuiltins();
        Assert.True(registry.IsRegistered("terminal"));
    }
}
