using Cove.Engine.Theming;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ThemeAnsiTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-theme-" + System.Guid.NewGuid().ToString("N"));

    [Fact]
    public void EveryBuiltin_CarriesSixteenValidAnsiColors()
    {
        var svc = new ThemeService(NewDir());
        foreach (var theme in svc.ListBuiltins())
        {
            Assert.NotNull(theme.Ansi);
            Assert.Equal(16, theme.Ansi!.Length);
            foreach (var hex in theme.Ansi)
                Assert.Matches("^#[0-9a-fA-F]{6}$", hex);
        }
    }

    [Fact]
    public void SaveCustom_RoundTripsAnsiPalette()
    {
        var dir = NewDir();
        var svc = new ThemeService(dir);
        var ansi = new string[16];
        for (var i = 0; i < 16; i++) ansi[i] = $"#0000{i:x2}";
        var theme = new Theme("my-theme", "dark", "#101010", "#e0e0e0", "#0a0a0a", "#e0e0e0", "#ff8800", ansi);
        svc.SaveCustom(theme);

        var reloaded = new ThemeService(dir).Get("my-theme");
        Assert.NotNull(reloaded);
        Assert.Equal(ansi, reloaded!.Ansi);
    }

    [Fact]
    public void SaveCustom_RejectsWrongLengthAnsi()
    {
        var svc = new ThemeService(NewDir());
        var theme = new Theme("bad", "dark", "#101010", "#e0e0e0", "#0a0a0a", "#e0e0e0", "#ff8800", ["#000000"]);
        Assert.Throws<System.ArgumentException>(() => svc.SaveCustom(theme));
    }

    [Fact]
    public void SetActiveIfKnown_FallsBackForUnknownName()
    {
        var svc = new ThemeService(NewDir());
        var applied = svc.SetActiveIfKnown("cove");
        Assert.NotNull(applied);
        Assert.Equal("catppuccin-mocha", applied!.Name);
        Assert.Equal(applied, svc.GetActive());
    }

    [Fact]
    public void SetActiveIfKnown_UsesTheNamedThemeWhenItExists()
    {
        var svc = new ThemeService(NewDir());
        var applied = svc.SetActiveIfKnown("cove-beacon");
        Assert.Equal("cove-beacon", applied!.Name);
    }
}
