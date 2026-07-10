using System.Text.Json;
using Cove.Engine.Theming;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ThemeCommandsTests
{
    private static string NewDataDir()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-theme-test-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task ThemeList_ReturnsAllBuiltins()
    {
        var dir = NewDataDir();
        try
        {
            var themes = new ThemeService(dir);
            var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/theme.list"), themes: themes);
            Assert.True(resp!.Ok);
            var arr = resp.Data!.Value.GetProperty("themes");
            Assert.Equal(7, arr.GetArrayLength());
            var first = arr[0];
            Assert.Equal("catppuccin-mocha", first.GetProperty("name").GetString());
            Assert.Equal("dark", first.GetProperty("type").GetString());
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task ThemeGetActive_ReturnsNullBeforeSet()
    {
        var dir = NewDataDir();
        try
        {
            var themes = new ThemeService(dir);
            var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/theme.get-active"), themes: themes);
            Assert.True(resp!.Ok);
            Assert.True(resp.Data!.Value.TryGetProperty("theme", out var themeProp) == false || themeProp.ValueKind == JsonValueKind.Null);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task ThemeSetActive_ReturnsThemeAndPersists()
    {
        var dir = NewDataDir();
        try
        {
            var themes = new ThemeService(dir);
            var prm = JsonDocument.Parse("""{"name":"cove-daybreak"}""").RootElement.Clone();
            var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/theme.set-active", prm), themes: themes);
            Assert.True(resp!.Ok);
            Assert.Equal("cove-daybreak", resp.Data!.Value.GetProperty("theme").GetProperty("name").GetString());

            var getResp = await EngineCommandRouter.RouteAsync(new ControlRequest("r2", "cove://commands/theme.get-active"), themes: themes);
            Assert.True(getResp!.Ok);
            Assert.Equal("cove-daybreak", getResp.Data!.Value.GetProperty("theme").GetProperty("name").GetString());
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task ThemeSaveCustom_AddsToListAndPersistsToDisk()
    {
        var dir = NewDataDir();
        try
        {
            var themes = new ThemeService(dir);
            var prm = JsonDocument.Parse("""{"name":"my-theme","type":"dark","terminalBackground":"#000000","terminalForeground":"#ffffff","chromeSurface":"#111111","chromeText":"#eeeeee","chromeAccent":"#ff0000"}""").RootElement.Clone();
            var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/theme.save-custom", prm), themes: themes);
            Assert.True(resp!.Ok);

            var listResp = await EngineCommandRouter.RouteAsync(new ControlRequest("r2", "cove://commands/theme.list"), themes: themes);
            var arr = listResp!.Data!.Value.GetProperty("themes");
            Assert.Equal(8, arr.GetArrayLength());
            Assert.Contains(arr.EnumerateArray(), t => t.GetProperty("name").GetString() == "my-theme");

            var reloaded = new ThemeService(dir);
            Assert.NotNull(reloaded.Get("my-theme"));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task ThemeDeleteCustom_RemovesCustomTheme()
    {
        var dir = NewDataDir();
        try
        {
            var themes = new ThemeService(dir);
            var theme = new Theme("to-delete", "dark", "#000000", "#ffffff", "#111111", "#eeeeee", "#ff0000");
            themes.SaveCustom(theme);

            var prm = JsonDocument.Parse("""{"name":"to-delete"}""").RootElement.Clone();
            var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/theme.delete-custom", prm), themes: themes);
            Assert.True(resp!.Ok);
            Assert.Null(themes.Get("to-delete"));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task ThemeDeleteCustom_RejectsBuiltin()
    {
        var dir = NewDataDir();
        try
        {
            var themes = new ThemeService(dir);
            var prm = JsonDocument.Parse("""{"name":"cove-harbor"}""").RootElement.Clone();
            var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/theme.delete-custom", prm), themes: themes);
            Assert.False(resp!.Ok);
            Assert.Equal("not_found", resp.Error!.Code);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task ThemeIsBuiltin_ReturnsTrueForBuiltin()
    {
        var dir = NewDataDir();
        try
        {
            var themes = new ThemeService(dir);
            var prm = JsonDocument.Parse("""{"name":"cove-midnight"}""").RootElement.Clone();
            var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/theme.is-builtin", prm), themes: themes);
            Assert.True(resp!.Ok);
            Assert.True(resp.Data!.Value.GetProperty("isBuiltin").GetBoolean());
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task ThemeIsBuiltin_ReturnsFalseForCustom()
    {
        var dir = NewDataDir();
        try
        {
            var themes = new ThemeService(dir);
            var theme = new Theme("custom-one", "light", "#ffffff", "#000000", "#eeeeee", "#111111", "#00ff00");
            themes.SaveCustom(theme);

            var prm = JsonDocument.Parse("""{"name":"custom-one"}""").RootElement.Clone();
            var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/theme.is-builtin", prm), themes: themes);
            Assert.True(resp!.Ok);
            Assert.False(resp.Data!.Value.GetProperty("isBuiltin").GetBoolean());
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task ThemeList_WithoutService_ReturnsNotReady()
    {
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/theme.list"));
        Assert.False(resp!.Ok);
        Assert.Equal("not_ready", resp.Error!.Code);
    }
}
