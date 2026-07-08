using Cove.Engine.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ThemeServiceTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-theme-{System.Guid.NewGuid():N}");

    [Fact]
    public void ListBuiltins_Returns6Themes()
    {
        var svc = new ThemeService(NewDir());
        var builtins = svc.ListBuiltins();
        Assert.Equal(6, builtins.Count);
    }

    [Fact]
    public void ListBuiltins_ContainsExpectedNames()
    {
        var svc = new ThemeService(NewDir());
        var names = svc.ListBuiltins().Select(t => t.Name).ToList();
        Assert.Contains("cove-harbor", names);
        Assert.Contains("cove-daybreak", names);
        Assert.Contains("cove-midnight", names);
        Assert.Contains("cove-shoal", names);
        Assert.Contains("cove-beacon", names);
        Assert.Contains("cove-chalk", names);
    }

    [Fact]
    public void Get_ReturnsThemeByName()
    {
        var svc = new ThemeService(NewDir());
        var theme = svc.Get("cove-harbor");
        Assert.NotNull(theme);
        Assert.Equal("dark", theme!.Type);
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
        svc.SetActive("cove-harbor");
        Assert.Equal("cove-harbor", svc.GetActive()!.Name);
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
        Assert.False(svc.DeleteCustom("cove-harbor"));
    }

    [Fact]
    public void IsBuiltin_RecognizesBuiltins()
    {
        var svc = new ThemeService(NewDir());
        Assert.True(svc.IsBuiltin("cove-harbor"));
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
    public void CoveHarbor_TextOnSurface_MeetsAA()
    {
        var ratio = ContrastValidator.ComputeContrastRatio("#e5e9f0", "#0b1622");
        Assert.True(ContrastValidator.MeetsAA(ratio), $"cove-harbor contrast {ratio:F2} < 4.5");
    }

    [Fact]
    public void CoveBeacon_TextOnSurface_MeetsAAA()
    {
        var ratio = ContrastValidator.ComputeContrastRatio("#f0f0f0", "#0d1117");
        Assert.True(ContrastValidator.MeetsAAA(ratio), $"cove-beacon contrast {ratio:F2} < 7.0");
    }

    [Fact]
    public void CoveChalk_TextOnSurface_MeetsAAA()
    {
        var ratio = ContrastValidator.ComputeContrastRatio("#1a1a1a", "#fafafa");
        Assert.True(ContrastValidator.MeetsAAA(ratio), $"cove-chalk contrast {ratio:F2} < 7.0");
    }

    [Fact]
    public void CoveDaybreak_TextOnSurface_MeetsAA()
    {
        var ratio = ContrastValidator.ComputeContrastRatio("#1a1a2e", "#ffffff");
        Assert.True(ContrastValidator.MeetsAA(ratio), $"cove-daybreak contrast {ratio:F2} < 4.5");
    }

    [Fact]
    public void CoveMidnight_TextOnSurface_MeetsAA()
    {
        var ratio = ContrastValidator.ComputeContrastRatio("#c0c0d0", "#0a0a1a");
        Assert.True(ContrastValidator.MeetsAA(ratio), $"cove-midnight contrast {ratio:F2} < 4.5");
    }

    [Fact]
    public void CoveShoal_TextOnSurface_MeetsAA()
    {
        var ratio = ContrastValidator.ComputeContrastRatio("#2d2418", "#f5f0e8");
        Assert.True(ContrastValidator.MeetsAA(ratio), $"cove-shoal contrast {ratio:F2} < 4.5");
    }

    [Fact]
    public void MeetsAALarge_Requires3to1()
    {
        Assert.True(ContrastValidator.MeetsAALarge(3.5));
        Assert.False(ContrastValidator.MeetsAALarge(2.5));
    }
}
