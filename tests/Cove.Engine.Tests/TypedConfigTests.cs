using System.IO;
using System.Text.Json;
using Cove.Engine.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class TypedConfigTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-tcfg-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Theme_DefaultsToCatppuccinMocha()
    {
        var dir = NewDir();
        try
        {
            var cfg = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal("catppuccin-mocha", cfg.GetTheme());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void TerminalFontSize_DefaultsToEleven()
    {
        var dir = NewDir();
        try
        {
            var cfg = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal("11", cfg.Get("terminal.fontSize"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void TypedSet_PersistsAndReloads()
    {
        var dir = NewDir();
        try
        {
            var cfg = new ConfigService(dir, NullLogger.Instance);
            cfg.SetTheme("dracula");
            cfg.SetTerminalFontSize(16);
            var cfg2 = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal("dracula", cfg2.GetTheme());
            Assert.Equal("16", cfg2.Get("terminal.fontSize"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void NonStringJsonValue_RoundTripsViaGet()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "config.json");
            File.WriteAllText(path, "{\"terminal\":{\"fontSize\":14},\"theme\":\"catppuccin\"}");
            var cfg = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal("14", cfg.Get("terminal.fontSize"));
            Assert.Equal("catppuccin", cfg.GetTheme());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void UnknownKey_PreservedInExtraBucket()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "config.json");
            File.WriteAllText(path, "{\"theme\":\"dracula\",\"futureSetting\":42}");
            var cfg = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal("42", cfg.Get("futureSetting"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void CorruptFile_BlocksWritesAndFallsBackToDefaults()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "config.json");
            File.WriteAllText(path, "{ this is not valid json");
            var cfg = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal("catppuccin-mocha", cfg.GetTheme());
            Assert.False(cfg.IsWritable());
            cfg.SetTheme("dracula");
            Assert.False(cfg.IsWritable());
            var raw = File.ReadAllText(path);
            Assert.DoesNotContain("dracula", raw);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void SetAutoDetect_IntValue()
    {
        var dir = NewDir();
        try
        {
            var cfg = new ConfigService(dir, NullLogger.Instance);
            cfg.Set("terminal.fontSize", "16");
            Assert.Equal("16", cfg.Get("terminal.fontSize"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void SetAutoDetect_BoolValue()
    {
        var dir = NewDir();
        try
        {
            var cfg = new ConfigService(dir, NullLogger.Instance);
            cfg.Set("diagnostics.enabled", "true");
            Assert.Equal("true", cfg.Get("diagnostics.enabled"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void CorruptFileRecovers_OnceFixed()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "config.json");
            File.WriteAllText(path, "{ broken");
            var cfg = new ConfigService(dir, NullLogger.Instance);
            Assert.False(cfg.IsWritable());
            File.WriteAllText(path, "{\"theme\":\"fixed\"}");
            cfg.Reload();
            Assert.True(cfg.IsWritable());
            Assert.Equal("fixed", cfg.GetTheme());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void AllSurvivingTopLevelKeys_HaveCanonicalGetters()
    {
        var dir = NewDir();
        try
        {
            var cfg = new ConfigService(dir, NullLogger.Instance);
            Assert.NotNull(cfg.GetTheme());
            Assert.NotNull(cfg.Get("terminal.fontFamily"));
            Assert.NotNull(cfg.Get("terminal.fontSize"));
            cfg.GetDiagnosticsEnabled();
            cfg.GetWorktreeDefaultLocationPattern();
            cfg.GetKeybindings();
            cfg.GetLspServerEntries();
            cfg.GetSessionRestoreOnLaunch();
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void OldStringEncodedFormat_CoercedNotLost()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "config.json");
            File.WriteAllText(path, "{\"theme\":\"dracula\",\"terminal\":{\"fontSize\":\"16\",\"lineHeight\":\"1.5\"}}");
            var cfg = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal("dracula", cfg.GetTheme());
            Assert.Equal("16", cfg.Get("terminal.fontSize"));
            cfg.Set("updates.checkOnLaunch", "false");
            var cfg2 = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal("16", cfg2.Get("terminal.fontSize"));
            Assert.Equal("dracula", cfg2.GetTheme());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void OldFlatDottedFormat_RoutedNotLost()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "config.json");
            File.WriteAllText(path, "{\"terminal.fontSize\":\"16\",\"updates.checkOnLaunch\":\"false\"}");
            var cfg = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal("16", cfg.Get("terminal.fontSize"));
            Assert.Equal("false", cfg.Get("updates.checkOnLaunch"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void RemovedSections_RoundTripHarmlesslyViaExtra()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "config.json");
            File.WriteAllText(path, "{\"theme\":\"dracula\",\"telemetry\":{\"enabled\":true},\"pushToTalk\":{\"keyCode\":99}}");
            var cfg = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal("dracula", cfg.GetTheme());
            Assert.Null(cfg.Get("telemetry.enabled"));
            Assert.Null(cfg.Get("pushToTalk.keyCode"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
