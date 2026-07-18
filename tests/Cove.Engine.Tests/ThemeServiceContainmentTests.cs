using Cove.Engine.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ThemeServiceContainmentTests
{
    private static string NewDataDir()
    {
        var dataDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-theme-containment-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dataDir);
        return dataDir;
    }

    private static Theme TraversalTheme() => new(
        "../config",
        "dark",
        "#000000",
        "#ffffff",
        "#0a0a0a",
        "#f0f0f0",
        "#ff0000");

    [Fact]
    public void SaveCustom_TraversalNameThrowsAndPreservesOutsideSentinel()
    {
        var dataDir = NewDataDir();
        var sentinelPath = System.IO.Path.Combine(dataDir, "config.json");
        System.IO.File.WriteAllText(sentinelPath, "sentinel");
        var service = new ThemeService(dataDir, NullLogger.Instance);

        Assert.Throws<System.ArgumentException>(() => service.SaveCustom(TraversalTheme()));

        Assert.Equal("sentinel", System.IO.File.ReadAllText(sentinelPath));
        Assert.Empty(service.ListCustom());
    }

    [Fact]
    public void DeleteCustom_TraversalNameReturnsFalseAndPreservesOutsideSentinel()
    {
        var dataDir = NewDataDir();
        var sentinelPath = System.IO.Path.Combine(dataDir, "config.json");
        System.IO.File.WriteAllText(sentinelPath, "sentinel");
        var service = new ThemeService(dataDir, NullLogger.Instance);

        Assert.False(service.DeleteCustom("../config"));

        Assert.Equal("sentinel", System.IO.File.ReadAllText(sentinelPath));
    }

    [Fact]
    public void LoadCustomThemes_UnsafeEmbeddedNameIsSkipped()
    {
        var dataDir = NewDataDir();
        var themesDir = System.IO.Path.Combine(dataDir, "themes");
        System.IO.Directory.CreateDirectory(themesDir);
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(themesDir, "tampered.json"),
            """{"name":"../config","type":"dark","terminalBackground":"#000000","terminalForeground":"#ffffff","chromeSurface":"#0a0a0a","chromeText":"#f0f0f0","chromeAccent":"#ff0000"}""");

        var service = new ThemeService(dataDir, NullLogger.Instance);

        Assert.Empty(service.ListCustom());
        Assert.Null(service.Get("../config"));
    }
}
