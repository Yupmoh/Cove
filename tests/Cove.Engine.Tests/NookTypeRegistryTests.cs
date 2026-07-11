using Cove.Engine.Nooks;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NookTypeRegistryTests
{
    [Fact]
    public void Register_AddsNookType()
    {
        var registry = new NookTypeRegistry();
        registry.Register(new NookTypeDefinition("editor", "Editor", "filesystem", IsDockable: true));
        Assert.True(registry.IsRegistered("editor"));
    }

    [Fact]
    public void Get_ReturnsDefinition()
    {
        var registry = new NookTypeRegistry();
        registry.Register(new NookTypeDefinition("editor", "Editor", "filesystem", IsDockable: true));
        var def = registry.Get("editor");
        Assert.NotNull(def);
        Assert.Equal("Editor", def!.DisplayName);
        Assert.Equal("filesystem", def.ContentSource);
    }

    [Fact]
    public void Get_Unknown_ReturnsNull()
    {
        var registry = new NookTypeRegistry();
        Assert.Null(registry.Get("nonexistent"));
    }

    [Fact]
    public void List_ReturnsAllRegistered()
    {
        var registry = new NookTypeRegistry();
        registry.Register(new NookTypeDefinition("editor", "Editor", "filesystem", true));
        registry.Register(new NookTypeDefinition("markdown", "Markdown", "note", true));
        registry.Register(new NookTypeDefinition("image", "Image", "file", true));
        Assert.Equal(3, registry.List().Count);
    }

    [Fact]
    public void BuiltIn_RegisterAll_DefaultTypes()
    {
        var registry = NookTypeRegistry.CreateWithBuiltins();
        Assert.True(registry.IsRegistered("terminal"));
        Assert.True(registry.IsRegistered("editor"));
        Assert.True(registry.IsRegistered("markdown"));
        Assert.True(registry.IsRegistered("image"));
        Assert.True(registry.IsRegistered("diff"));
        Assert.True(registry.IsRegistered("git"));
    }

    [Fact]
    public void Unregister_RemovesType()
    {
        var registry = new NookTypeRegistry();
        registry.Register(new NookTypeDefinition("editor", "Editor", "filesystem", true));
        registry.Unregister("editor");
        Assert.False(registry.IsRegistered("editor"));
    }

    [Fact]
    public void IsDockable_ReturnsFlag()
    {
        var registry = new NookTypeRegistry();
        registry.Register(new NookTypeDefinition("editor", "Editor", "filesystem", IsDockable: true));
        registry.Register(new NookTypeDefinition("terminal", "Terminal", "pty", IsDockable: false));
        Assert.True(registry.IsDockable("editor"));
        Assert.False(registry.IsDockable("terminal"));
    }
}
