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
    public void Theme_DefaultsToCove()
    {
        var dir = NewDir();
        try
        {
            var cfg = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal("cove", cfg.GetTheme());
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
            Assert.Equal(11, cfg.GetTerminalFontSize());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void TerminalFontLigatures_DefaultsFalse()
    {
        var dir = NewDir();
        try
        {
            var cfg = new ConfigService(dir, NullLogger.Instance);
            Assert.False(cfg.GetTerminalFontLigatures());
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
            cfg.SetTerminalFontLigatures(true);
            var cfg2 = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal("dracula", cfg2.GetTheme());
            Assert.Equal(16, cfg2.GetTerminalFontSize());
            Assert.True(cfg2.GetTerminalFontLigatures());
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
            File.WriteAllText(path, "{\"terminal\":{\"fontSize\":14,\"fontLigatures\":true},\"theme\":\"catppuccin\"}");
            var cfg = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal(14, cfg.GetTerminalFontSize());
            Assert.True(cfg.GetTerminalFontLigatures());
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
            File.WriteAllText(path, "{\"theme\":\"cove\",\"future.unknown.key\":\"preserved\"}");
            var cfg = new ConfigService(dir, NullLogger.Instance);
            cfg.SetTheme("dracula");
            var raw = File.ReadAllText(path);
            Assert.Contains("\"future.unknown.key\":", raw);
            Assert.Contains("preserved", raw);
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
            Assert.Equal("cove", cfg.GetTheme());
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
            Assert.Equal(16, cfg.GetTerminalFontSize());
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
            cfg.Set("terminal.fontLigatures", "true");
            Assert.True(cfg.GetTerminalFontLigatures());
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
    public void AllThirteenTopLevelKeys_HaveCanonicalGetters()
    {
        var dir = NewDir();
        try
        {
            var cfg = new ConfigService(dir, NullLogger.Instance);
            Assert.NotNull(cfg.GetTheme());
            Assert.NotNull(cfg.GetTerminalFontFamily());
            Assert.True(cfg.GetTerminalFontSize() > 0);
            cfg.GetUpdatesChannel();
            cfg.GetTelemetryEnabled();
            cfg.GetDiagnosticsEnabled();
            cfg.GetWorktreeDefaultLocationPattern();
            cfg.GetMarkdownEditorDefaultFont();
            cfg.GetKeybindings();
            cfg.GetRemoteConfigDismissedBannerIds();
            cfg.GetPushToTalkEnabled();
            cfg.GetSpeechGain();
            cfg.GetLspServers();
            cfg.GetAdapterCommands();
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
            File.WriteAllText(path, "{\"theme\":\"dracula\",\"terminal\":{\"fontSize\":\"16\",\"fontLigatures\":\"true\",\"lineHeight\":\"1.5\"}}");
            var cfg = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal("dracula", cfg.GetTheme());
            Assert.Equal(16, cfg.GetTerminalFontSize());
            Assert.True(cfg.GetTerminalFontLigatures());
            cfg.Set("updates.channel", "beta");
            var cfg2 = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal(16, cfg2.GetTerminalFontSize());
            Assert.True(cfg2.GetTerminalFontLigatures());
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
            File.WriteAllText(path, "{\"terminal.fontSize\":\"16\",\"terminal.fontLigatures\":\"true\",\"updates.channel\":\"beta\"}");
            var cfg = new ConfigService(dir, NullLogger.Instance);
            Assert.Equal(16, cfg.GetTerminalFontSize());
            Assert.True(cfg.GetTerminalFontLigatures());
            Assert.Equal("beta", cfg.GetUpdatesChannel());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
