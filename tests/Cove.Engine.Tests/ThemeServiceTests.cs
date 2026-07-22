using Cove.Engine.Config;
using Cove.Engine.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ThemeServiceTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-theme-{System.Guid.NewGuid():N}");

    [Fact]
    public void ListBuiltins_ReturnsOnlyCatppuccinMocha()
    {
        var svc = new ThemeService(NewDir());
        var builtins = svc.ListBuiltins();
        Assert.Single(builtins);
    }

    [Fact]
    public void ListBuiltins_ContainsOnlyCatppuccinMocha()
    {
        var svc = new ThemeService(NewDir());
        var theme = Assert.Single(svc.ListBuiltins());
        Assert.Equal("catppuccin-mocha", theme.Name);
    }

    [Fact]
    public void ThemeSetting_OffersOnlyCatppuccinMocha()
    {
        var entry = Assert.Single(ConfigSchemaGenerator.Generate(), item => item.Key == "theme");
        Assert.Equal(["catppuccin-mocha"], Assert.IsType<string[]>(entry.Options));
    }

    [Fact]
    public void CatppuccinMocha_HasExpectedPalette()
    {
        var svc = new ThemeService(NewDir());
        var theme = svc.Get("catppuccin-mocha");
        Assert.NotNull(theme);
        Assert.Equal("dark", theme!.Type);
        Assert.Equal("#1e1e2e", theme.TerminalBackground);
        Assert.Equal("#cdd6f4", theme.TerminalForeground);
        Assert.Equal("#181825", theme.ChromeSurface);
        Assert.Equal("#cba6f7", theme.ChromeAccent);
    }

    [Fact]
    public void Get_RemovedBuiltin_ReturnsNull()
    {
        var svc = new ThemeService(NewDir());
        Assert.Null(svc.Get("cove-harbor"));
    }

    [Fact]
    public void Get_Nonexistent_ReturnsNull()
    {
        var svc = new ThemeService(NewDir());
        Assert.Null(svc.Get("nonexistent"));
    }

    [Fact]
    public void SetActive_SetsActiveTheme()
    {
        var svc = new ThemeService(NewDir());
        svc.SetActive("catppuccin-mocha");
        Assert.Equal("catppuccin-mocha", svc.GetActive()!.Name);
    }

    [Fact]
    public void SetActive_Nonexistent_Throws()
    {
        var svc = new ThemeService(NewDir());
        Assert.Throws<System.ArgumentException>(() => svc.SetActive("nonexistent"));
    }

    [Fact]
    public void SaveCustom_PersistsToDisk()
    {
        var dir = NewDir();
        var svc = new ThemeService(dir);
        var theme = new Theme("my-custom", "dark", "#000000", "#ffffff", "#0a0a0a", "#f0f0f0", "#ff0000");
        svc.SaveCustom(theme);

        var svc2 = new ThemeService(dir);
        Assert.NotNull(svc2.Get("my-custom"));
    }

    [Fact]
    public void DeleteCustom_RemovesFromList()
    {
        var dir = NewDir();
        var svc = new ThemeService(dir);
        var theme = new Theme("to-delete", "dark", "#000000", "#ffffff", "#0a0a0a", "#f0f0f0", "#ff0000");
        svc.SaveCustom(theme);
        Assert.True(svc.DeleteCustom("to-delete"));
        Assert.Null(svc.Get("to-delete"));
    }

    [Fact]
    public void DeleteCustom_Builtin_ReturnsFalse()
    {
        var svc = new ThemeService(NewDir());
        Assert.False(svc.DeleteCustom("catppuccin-mocha"));
    }

    [Fact]
    public void IsBuiltin_RecognizesOnlyCatppuccinMocha()
    {
        var svc = new ThemeService(NewDir());
        Assert.True(svc.IsBuiltin("catppuccin-mocha"));
        Assert.False(svc.IsBuiltin("cove-harbor"));
        Assert.False(svc.IsBuiltin("my-custom"));
    }

    [Fact]
    public void LoadFromJson_ValidTheme_ReturnsTheme()
    {
        var svc = new ThemeService(NewDir());
        var json = """{"name":"test","type":"dark","terminalBackground":"#000000","terminalForeground":"#ffffff","chromeSurface":"#0a0a0a","chromeText":"#f0f0f0","chromeAccent":"#ff0000"}""";
        var theme = svc.LoadFromJson(json);
        Assert.Equal("test", theme.Name);
    }

    [Fact]
    public void LoadFromJson_InvalidType_Throws()
    {
        var svc = new ThemeService(NewDir());
        var json = """{"name":"test","type":"invalid","terminalBackground":"#000000","terminalForeground":"#ffffff","chromeSurface":"#0a0a0a","chromeText":"#f0f0f0","chromeAccent":"#ff0000"}""";
        Assert.Throws<System.ArgumentException>(() => svc.LoadFromJson(json));
    }

    [Fact]
    public void LoadFromJson_InvalidHex_Throws()
    {
        var svc = new ThemeService(NewDir());
        var json = """{"name":"test","type":"dark","terminalBackground":"not-a-color","terminalForeground":"#ffffff","chromeSurface":"#0a0a0a","chromeText":"#f0f0f0","chromeAccent":"#ff0000"}""";
        Assert.Throws<System.ArgumentException>(() => svc.LoadFromJson(json));
    }
}

public sealed class ContrastValidatorTests
{
    [Fact]
    public void BlackOnWhite_HasMaximumContrast()
    {
        var ratio = ContrastValidator.ComputeContrastRatio("#000000", "#ffffff");
        Assert.True(ratio >= 20.0);
    }

    [Fact]
    public void SameColor_HasMinimumContrast()
    {
        var ratio = ContrastValidator.ComputeContrastRatio("#888888", "#888888");
        Assert.Equal(1.0, ratio, 1);
    }

    [Fact]
    public void CatppuccinMocha_TextOnSurface_MeetsAA()
    {
        var ratio = ContrastValidator.ComputeContrastRatio("#cdd6f4", "#181825");
        Assert.True(ContrastValidator.MeetsAA(ratio), $"catppuccin-mocha contrast {ratio:F2} < 4.5");
    }







    [Fact]
    public void MeetsAALarge_Requires3to1()
    {
        Assert.True(ContrastValidator.MeetsAALarge(3.5));
        Assert.False(ContrastValidator.MeetsAALarge(2.5));
    }
}
