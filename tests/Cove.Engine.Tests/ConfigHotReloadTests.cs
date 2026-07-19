using System.IO;
using System.Threading;
using Cove.Engine.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ConfigHotReloadTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-cfg-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Set_ThenGet_RoundTrips()
    {
        var dir = NewDir();
        try
        {
            var cfg = new ConfigService(dir, NullLogger.Instance);
            cfg.Set("terminal.fontSize", "14");
            Assert.Equal("14", cfg.Get("terminal.fontSize"));
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Set_Bool_PreservesType()
    {
        var dir = NewDir();
        try
        {
            var cfg = new ConfigService(dir, NullLogger.Instance);
            cfg.Set("ui.animations", "true");
            Assert.Equal("true", cfg.Get("ui.animations"));
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Set_PersistsAcrossInstances()
    {
        var dir = NewDir();
        try
        {
            var cfg1 = new ConfigService(dir, NullLogger.Instance);
            cfg1.Set("terminal.fontFamily", "FiraCode");
            var cfg2 = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal("FiraCode", cfg2.Get("terminal.fontFamily"));
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Set_FiresSettingsChanged()
    {
        var dir = NewDir();
        try
        {
            var cfg = new ConfigService(dir, NullLogger.Instance);
            string changedKey = "";
            cfg.SettingsChanged += key => changedKey = key;
            cfg.Set("terminal.fontSize", "14");
            Assert.Equal("terminal.fontSize", changedKey);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Load_PicksUpExternalFileChange()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "config.json");
            File.WriteAllText(path, "{\"terminal\":{\"fontSize\":12}}");
            var cfg = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal("12", cfg.Get("terminal.fontSize"));

            File.WriteAllText(path, "{\"terminal\":{\"fontSize\":14}}");
            cfg.Reload();
            Assert.Equal("14", cfg.Get("terminal.fontSize"));
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public async Task HotReload_AtomicWrite_FiresSettingsChanged()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        ConfigService? cfg = null;
        try
        {
            var path = Path.Combine(dir, "config.json");
            File.WriteAllText(path, "{\"terminal\":{\"fontSize\":12}}");
            cfg = new ConfigService(dir, NullLogger.Instance);
            cfg.StartWatching();
            var changedKeys = new System.Collections.Concurrent.ConcurrentBag<string>();
            cfg.SettingsChanged += key => changedKeys.Add(key);

            var cfg2 = new ConfigService(dir, NullLogger.Instance);
            cfg2.Set("terminal.fontSize", "14");

            await Cove.Testing.AsyncTest.EventuallyAsync(
                () => cfg.Get("terminal.fontSize") == "14",
                TimeSpan.FromSeconds(30),
                "hot-reloaded font size was not observed");
            await Cove.Testing.AsyncTest.EventuallyAsync(
                () => changedKeys.Contains("terminal.fontSize"),
                TimeSpan.FromSeconds(30),
                "settings-changed event was not observed");

            Assert.Equal("14", cfg.Get("terminal.fontSize"));
            Assert.Contains("terminal.fontSize", changedKeys);
        }
        finally { cfg?.Dispose(); Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void CorruptFile_FallsBackSafely()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "config.json");
            File.WriteAllText(path, "{ this is not valid json");
            var cfg = new ConfigService(dir, NullLogger.Instance);
            Assert.False(cfg.IsWritable());
            Assert.Equal("11", cfg.Get("terminal.fontSize"));
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void UnknownKey_PreservedAcrossRewrite()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "config.json");
            File.WriteAllText(path, "{\"terminal.fontSize\":\"12\",\"custom.unknown\":\"preserved\"}");
            var cfg = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal("preserved", cfg.Get("custom.unknown"));
            cfg.Set("terminal.fontSize", "14");
            var cfg2 = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal("preserved", cfg2.Get("custom.unknown"));
            Assert.Equal("14", cfg2.Get("terminal.fontSize"));
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void AutoDetectType_IntStoredAsString()
    {
        var dir = NewDir();
        try
        {
            var cfg = new ConfigService(dir, NullLogger.Instance);
            cfg.Set("terminal.fontSize", "14");
            var raw = File.ReadAllText(Path.Combine(dir, "config.json"));
            Assert.Contains("\"fontSize\": 14", raw);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void AppearanceSettings_RoundTripAcrossInstances()
    {
        var dir = NewDir();
        try
        {
            var cfg1 = new ConfigService(dir, NullLogger.Instance);
            cfg1.Set("appearance.uiScale", "1.25");
            cfg1.Set("appearance.layoutGap", "6");
            cfg1.Set("appearance.iconSet", "outline");
            cfg1.Set("appearance.wallpaper", "/path/to/wp.png");
            cfg1.Set("appearance.accent", "#ff5500");
            cfg1.Set("appearance.nookLight", "true");

            var cfg2 = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal("1.25", cfg2.Get("appearance.uiScale"));
            Assert.Equal("6", cfg2.Get("appearance.layoutGap"));
            Assert.Equal("outline", cfg2.Get("appearance.iconSet"));
            Assert.Equal("/path/to/wp.png", cfg2.Get("appearance.wallpaper"));
            Assert.Equal("#ff5500", cfg2.Get("appearance.accent"));
            Assert.Equal("true", cfg2.Get("appearance.nookLight"));
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void AppearanceSettings_DefaultsWhenAbsent()
    {
        var dir = NewDir();
        try
        {
            var cfg = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal("1", cfg.Get("appearance.uiScale"));
            Assert.Equal("4", cfg.Get("appearance.layoutGap"));
            Assert.Equal("default", cfg.Get("appearance.iconSet"));
            Assert.Equal("", cfg.Get("appearance.wallpaper"));
            Assert.Equal("", cfg.Get("appearance.accent"));
            Assert.Equal("false", cfg.Get("appearance.nookLight"));
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void OnboardingCompleted_RoundTripSurvivesAutoDetectJson()
    {
        var dir = NewDir();
        try
        {
            var cfg1 = new ConfigService(dir, NullLogger.Instance);
            cfg1.Set("onboarding.completed", "true");
            var raw = File.ReadAllText(Path.Combine(dir, "config.json"));
            var cfg2 = new ConfigService(dir, NullLogger.Instance);
            var readBack = cfg2.Get("onboarding.completed");
            Assert.True(string.Equals(readBack, "true", System.StringComparison.Ordinal), $"expected exact 'true' but got '{readBack}' (raw: {raw})");
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }
}
