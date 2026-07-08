using Cove.Engine.Keybindings;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class KeybindingEngineTests
{
    [Fact]
    public void RegisterDefault_ThenResolve_ReturnsBinding()
    {
        var engine = new KeybindingEngine();
        engine.RegisterDefault("Cmd+T", "app-command", "room.new", "New room");

        var binding = engine.Resolve("cmd+t");
        Assert.NotNull(binding);
        Assert.Equal("room.new", binding!.Action);
        Assert.Equal("app-command", binding.ActionType);
    }

    [Fact]
    public void NormalizeChord_LowercasesAndOrders()
    {
        Assert.Equal("cmd+shift+t", KeybindingEngine.NormalizeChord("Shift+Cmd+T"));
        Assert.Equal("ctrl+alt+d", KeybindingEngine.NormalizeChord("Alt+Ctrl+D"));
        Assert.Equal("cmd+t", KeybindingEngine.NormalizeChord("CMD+T"));
    }

    [Fact]
    public void NormalizeChord_OptBecomesAlt()
    {
        Assert.Equal("alt+x", KeybindingEngine.NormalizeChord("Opt+X"));
    }

    [Fact]
    public void SetOverride_ReplacesDefault()
    {
        var engine = new KeybindingEngine();
        engine.RegisterDefault("Cmd+T", "app-command", "room.new", null);
        engine.SetOverride("Cmd+T", new KeyBinding("cmd+t", "app-command", "task.create", null));

        var binding = engine.Resolve("cmd+t");
        Assert.Equal("task.create", binding!.Action);
    }

    [Fact]
    public void SetOverride_NullUnbinds()
    {
        var engine = new KeybindingEngine();
        engine.RegisterDefault("Cmd+T", "app-command", "room.new", null);
        engine.SetOverride("Cmd+T", null);

        Assert.Null(engine.Resolve("cmd+t"));
    }

    [Fact]
    public void ClearOverride_RestoresDefault()
    {
        var engine = new KeybindingEngine();
        engine.RegisterDefault("Cmd+T", "app-command", "room.new", null);
        engine.SetOverride("Cmd+T", new KeyBinding("cmd+t", "app-command", "other", null));
        engine.ClearOverride("Cmd+T");

        Assert.Equal("room.new", engine.Resolve("cmd+t")!.Action);
    }

    [Fact]
    public void GetConflicts_DetectsTwoActionsSameChord()
    {
        var engine = new KeybindingEngine();
        engine.RegisterDefault("Cmd+T", "app-command", "room.new", null);
        engine.RegisterDefault("cmd+t", "app-command", "task.create", null);

        var conflicts = engine.GetConflicts();
        Assert.Single(conflicts);
        Assert.Contains("cmd+t", conflicts);
    }

    [Fact]
    public void GetConflicts_Empty_WhenDistinctChords()
    {
        var engine = new KeybindingEngine();
        engine.RegisterDefault("Cmd+T", "app-command", "room.new", null);
        engine.RegisterDefault("Cmd+W", "app-command", "room.close", null);

        var conflicts = engine.GetConflicts();
        Assert.Empty(conflicts);
    }

    [Fact]
    public void GetConflicts_Empty_WhenSameActionSameChord()
    {
        var engine = new KeybindingEngine();
        engine.RegisterDefault("Cmd+T", "app-command", "room.new", null);
        engine.RegisterDefault("cmd+t", "app-command", "room.new", null);

        var conflicts = engine.GetConflicts();
        Assert.Empty(conflicts);
    }

    [Fact]
    public void TrySetOverride_ReservedChord_ReturnsFalse()
    {
        var engine = new KeybindingEngine();
        var result = engine.TrySetOverride("Cmd+Q", new KeyBinding("cmd+q", "app-command", "app.quit", null));

        Assert.False(result.Success);
        Assert.Contains("reserved", result.Error ?? "", System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TrySetOverride_ReservedCmdTab_ReturnsFalse()
    {
        var engine = new KeybindingEngine();
        var result = engine.TrySetOverride("Cmd+Tab", new KeyBinding("cmd+tab", "app-command", "app.switch", null));

        Assert.False(result.Success);
    }

    [Fact]
    public void TrySetOverride_NonReservedChord_Succeeds()
    {
        var engine = new KeybindingEngine();
        var result = engine.TrySetOverride("Cmd+T", new KeyBinding("cmd+t", "app-command", "task.create", null));

        Assert.True(result.Success);
        Assert.Null(result.Error);
    }

    [Fact]
    public void TrySetOverride_Conflict_ReturnsWarningButApplies()
    {
        var engine = new KeybindingEngine();
        engine.RegisterDefault("Cmd+T", "app-command", "room.new", null);
        var result = engine.TrySetOverride("Cmd+T", new KeyBinding("cmd+t", "app-command", "task.create", null));

        Assert.True(result.Success);
        Assert.Contains("conflict", result.Warning ?? "", System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_SendTextBinding_ReturnsSendTextActionType()
    {
        var engine = new KeybindingEngine();
        engine.RegisterDefault("Cmd+Enter", "send-text", "ls -la\r", null);

        var binding = engine.Resolve("cmd+enter");
        Assert.NotNull(binding);
        Assert.Equal("send-text", binding!.ActionType);
        Assert.Equal("ls -la\r", binding.Action);
    }

    [Fact]
    public void Resolve_UriCommandBinding_ReturnsUriCommandActionType()
    {
        var engine = new KeybindingEngine();
        engine.RegisterDefault("Cmd+Shift+O", "uri-command", "cove://commands/room.new", null);

        var binding = engine.Resolve("cmd+shift+o");
        Assert.NotNull(binding);
        Assert.Equal("uri-command", binding!.ActionType);
    }

    [Fact]
    public void IsReserved_CmdQ_ReturnsTrue()
    {
        var engine = new KeybindingEngine();
        Assert.True(engine.IsReserved("Cmd+Q"));
        Assert.True(engine.IsReserved("cmd+q"));
    }

    [Fact]
    public void IsReserved_CmdT_ReturnsFalse()
    {
        var engine = new KeybindingEngine();
        Assert.False(engine.IsReserved("Cmd+T"));
    }

    [Fact]
    public void LoadFromJson_ParsesOverrides()
    {
        var engine = new KeybindingEngine();
        engine.RegisterDefault("Cmd+T", "app-command", "room.new", null);
        engine.LoadFromJson("""{"cmd+t":{"actionType":"app-command","action":"task.create"},"cmd+w":null}""");

        Assert.Equal("task.create", engine.Resolve("cmd+t")!.Action);
        Assert.Null(engine.Resolve("cmd+w"));
    }

    [Fact]
    public void ToJson_RoundTrips()
    {
        var engine = new KeybindingEngine();
        engine.SetOverride("Cmd+T", new KeyBinding("cmd+t", "app-command", "task.create", "desc"));
        engine.SetOverride("Cmd+W", null);

        var json = engine.ToJson();
        Assert.Contains("task.create", json);
        Assert.Contains("null", json);
    }

    [Fact]
    public void GetResolvedBindings_MergesDefaultsAndOverrides()
    {
        var engine = new KeybindingEngine();
        engine.RegisterDefault("Cmd+T", "app-command", "room.new", null);
        engine.RegisterDefault("Cmd+W", "app-command", "room.close", null);
        engine.SetOverride("Cmd+W", null);

        var bindings = engine.GetResolvedBindings();
        Assert.Single(bindings);
        Assert.Equal("room.new", bindings[0].Action);
    }
}
