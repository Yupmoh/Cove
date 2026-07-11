using System.Text.Json;
using Cove.Engine.Keybindings;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class KeybindingCommandsTests
{
    private static KeybindingEngine NewEngine()
    {
        var kb = new KeybindingEngine();
        DefaultKeymap.RegisterAll(kb);
        return kb;
    }

    [Fact]
    public async Task KeybindList_ReturnsAllDefaults()
    {
        var kb = NewEngine();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/keybind.list"), keybindings: kb);
        Assert.True(resp!.Ok);
        var bindings = resp.Data!.Value.GetProperty("bindings");
        Assert.True(bindings.GetArrayLength() >= 30);
        var first = bindings[0];
        Assert.True(first.TryGetProperty("chord", out _));
        Assert.True(first.TryGetProperty("action", out _));
    }

    [Fact]
    public async Task KeybindList_IncludesConflicts()
    {
        var kb = NewEngine();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/keybind.list"), keybindings: kb);
        Assert.True(resp!.Ok);
        Assert.True(resp.Data!.Value.TryGetProperty("conflicts", out _));
        var conflicts = resp.Data!.Value.GetProperty("conflicts");
        Assert.True(conflicts.GetArrayLength() > 0);
        Assert.Contains("cmd+shift+b", conflicts.EnumerateArray().Select(c => c.GetString()));
    }

    [Fact]
    public async Task KeybindSet_OverridesBinding()
    {
        var kb = NewEngine();
        var prm = JsonDocument.Parse("""{"chord":"cmd+t","actionType":"app-command","action":"shore.new","description":"New shore"}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/keybind.set", prm), keybindings: kb);
        Assert.True(resp!.Ok);
        Assert.True(resp.Data!.Value.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task KeybindSet_RejectsReservedChord()
    {
        var kb = NewEngine();
        var prm = JsonDocument.Parse("""{"chord":"cmd+q","actionType":"app-command","action":"app.quit"}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/keybind.set", prm), keybindings: kb);
        Assert.False(resp!.Ok);
        Assert.Equal("reserved", resp.Error!.Code);
    }

    [Fact]
    public async Task KeybindSet_ReturnsWarningOnConflict()
    {
        var kb = NewEngine();
        var prm = JsonDocument.Parse("""{"chord":"cmd+t","actionType":"app-command","action":"different.action"}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/keybind.set", prm), keybindings: kb);
        Assert.True(resp!.Ok);
        Assert.True(resp.Data!.Value.GetProperty("success").GetBoolean());
        Assert.True(resp.Data!.Value.TryGetProperty("warning", out var warning));
        var warningText = warning.ValueKind == JsonValueKind.String ? warning.GetString() : warning.GetProperty("warning").GetString();
        Assert.Contains("conflict", warningText ?? "");
    }

    [Fact]
    public async Task KeybindClear_RemovesOverride()
    {
        var kb = NewEngine();
        var setPrm = JsonDocument.Parse("""{"chord":"cmd+t","actionType":"app-command","action":"custom.action"}""").RootElement.Clone();
        await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/keybind.set", setPrm), keybindings: kb);

        var clearPrm = JsonDocument.Parse("""{"chord":"cmd+t"}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r2", "cove://commands/keybind.clear", clearPrm), keybindings: kb);
        Assert.True(resp!.Ok);

        var resolved = kb.Resolve("cmd+t");
        Assert.Equal("shore.new", resolved?.Action);
    }

    [Fact]
    public async Task KeybindConflicts_ReturnsConflictChords()
    {
        var kb = NewEngine();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/keybind.conflicts"), keybindings: kb);
        Assert.True(resp!.Ok);
        var conflicts = resp.Data!.Value.GetProperty("conflicts");
        Assert.True(conflicts.GetArrayLength() > 0);
    }

    [Fact]
    public async Task KeybindIsReserved_ReturnsTrueForReserved()
    {
        var kb = NewEngine();
        var prm = JsonDocument.Parse("""{"chord":"cmd+q"}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/keybind.is-reserved", prm), keybindings: kb);
        Assert.True(resp!.Ok);
        Assert.True(resp.Data!.Value.GetProperty("isReserved").GetBoolean());
    }

    [Fact]
    public async Task KeybindIsReserved_ReturnsFalseForNormal()
    {
        var kb = NewEngine();
        var prm = JsonDocument.Parse("""{"chord":"cmd+t"}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/keybind.is-reserved", prm), keybindings: kb);
        Assert.True(resp!.Ok);
        Assert.False(resp.Data!.Value.GetProperty("isReserved").GetBoolean());
    }

    [Fact]
    public async Task KeybindList_WithoutEngine_ReturnsNotReady()
    {
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/keybind.list"));
        Assert.False(resp!.Ok);
        Assert.Equal("not_ready", resp.Error!.Code);
    }
    [Fact]
    public async Task KeybindSet_PersistsAndReloadsAcrossInstances()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-keybind-test-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var config1 = new Cove.Engine.Config.ConfigService(dir, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
            var kb1 = NewEngine();
            var setPrm = JsonDocument.Parse("""{"chord":"cmd+shift+x","actionType":"app-command","action":"custom.action","description":"Custom"}""").RootElement.Clone();
            var setResp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/keybind.set", setPrm), keybindings: kb1, config: config1);
            Assert.True(setResp!.Ok);

            var config2 = new Cove.Engine.Config.ConfigService(dir, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
            var kb2 = new KeybindingEngine();
            var saved = config2.GetKeybindingsJson();
            Assert.NotNull(saved);
            kb2.LoadFromJson(saved!);
            var resolved = kb2.Resolve("cmd+shift+x");
            Assert.NotNull(resolved);
            Assert.Equal("custom.action", resolved!.Action);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task KeybindClear_PersistsRemovalAcrossInstances()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-keybind-test-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var config1 = new Cove.Engine.Config.ConfigService(dir, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
            var kb1 = NewEngine();
            var setPrm = JsonDocument.Parse("""{"chord":"cmd+shift+x","actionType":"app-command","action":"custom.action"}""").RootElement.Clone();
            await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/keybind.set", setPrm), keybindings: kb1, config: config1);

            var clearPrm = JsonDocument.Parse("""{"chord":"cmd+shift+x"}""").RootElement.Clone();
            var clearResp = await EngineCommandRouter.RouteAsync(new ControlRequest("r2", "cove://commands/keybind.clear", clearPrm), keybindings: kb1, config: config1);
            Assert.True(clearResp!.Ok);

            var config2 = new Cove.Engine.Config.ConfigService(dir, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
            var kb2 = new KeybindingEngine();
            DefaultKeymap.RegisterAll(kb2);
            var saved = config2.GetKeybindingsJson();
            if (!string.IsNullOrEmpty(saved)) kb2.LoadFromJson(saved!);
            var resolved = kb2.Resolve("cmd+shift+x");
            Assert.Null(resolved);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }
}
