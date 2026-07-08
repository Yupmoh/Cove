using Cove.Engine.Keybindings;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class DefaultKeymapTests
{
    [Fact]
    public void RegisterAll_RegistersRoomShortcuts()
    {
        var engine = new KeybindingEngine();
        DefaultKeymap.RegisterAll(engine);

        Assert.NotNull(engine.Resolve("cmd+t"));
        Assert.Equal("room.new", engine.Resolve("cmd+t")!.Action);
        Assert.NotNull(engine.Resolve("cmd+shift+w"));
        Assert.Equal("room.close", engine.Resolve("cmd+shift+w")!.Action);
    }

    [Fact]
    public void RegisterAll_RegistersWorkspaceShortcuts()
    {
        var engine = new KeybindingEngine();
        DefaultKeymap.RegisterAll(engine);

        for (var i = 1; i <= 9; i++)
        {
            var binding = engine.Resolve($"cmd+{i}");
            Assert.NotNull(binding);
            Assert.Equal($"workspace.switch-{i}", binding!.Action);
        }
    }

    [Fact]
    public void RegisterAll_RegistersViewShortcuts()
    {
        var engine = new KeybindingEngine();
        DefaultKeymap.RegisterAll(engine);

        Assert.Equal("view.toggle-sidebar", engine.Resolve("cmd+b")!.Action);
        Assert.Equal("view.zen-mode", engine.Resolve("cmd+shift+`")!.Action);
        Assert.Equal("view.zoom-in", engine.Resolve("cmd+=")!.Action);
    }

    [Fact]
    public void RegisterAll_RegistersToolPaneShortcuts()
    {
        var engine = new KeybindingEngine();
        DefaultKeymap.RegisterAll(engine);

        Assert.Equal("tool.search", engine.Resolve("cmd+shift+f")!.Action);
        Assert.Equal("tool.launcher", engine.Resolve("cmd+l")!.Action);
    }

    [Fact]
    public void RegisterAll_RegistersPaneShortcuts()
    {
        var engine = new KeybindingEngine();
        DefaultKeymap.RegisterAll(engine);

        Assert.Equal("pane.split-right", engine.Resolve("cmd+d")!.Action);
        Assert.Equal("pane.focus-next", engine.Resolve("cmd+]")!.Action);
        Assert.Equal("pane.find", engine.Resolve("cmd+f")!.Action);
    }

    [Fact]
    public void RegisterAll_RegistersTerminalShortcuts()
    {
        var engine = new KeybindingEngine();
        DefaultKeymap.RegisterAll(engine);

        Assert.Equal("terminal.copy-or-sigint", engine.Resolve("cmd+c")!.Action);
        Assert.Equal("terminal.select-all", engine.Resolve("cmd+a")!.Action);
    }

    [Fact]
    public void RegisterAll_RegistersSendTextAction()
    {
        var engine = new KeybindingEngine();
        DefaultKeymap.RegisterAll(engine);

        var binding = engine.Resolve("shift+enter");
        Assert.NotNull(binding);
        Assert.Equal("send-text", binding!.ActionType);
    }

    [Fact]
    public void RegisterAll_AllBindingsAreReassignable()
    {
        var engine = new KeybindingEngine();
        DefaultKeymap.RegisterAll(engine);

        engine.SetOverride("cmd+t", new KeyBinding("cmd+t", "app-command", "custom.action", null));
        Assert.Equal("custom.action", engine.Resolve("cmd+t")!.Action);
    }

    [Fact]
    public void RegisterAll_AllBindingsHaveDescriptions()
    {
        var engine = new KeybindingEngine();
        DefaultKeymap.RegisterAll(engine);

        var resolved = engine.GetResolvedBindings();
        foreach (var binding in resolved)
        {
            Assert.False(string.IsNullOrEmpty(binding.Description), $"binding {binding.Action} has no description");
        }
    }
}
