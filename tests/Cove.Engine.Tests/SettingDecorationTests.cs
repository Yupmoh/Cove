using Cove.Engine.Config;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SettingDecorationTests
{
    [Fact]
    public void SettingAttribute_Defaults_ControlIsText()
    {
        var attr = new SettingAttribute("Theme", "appearance");
        Assert.Equal("text", attr.Control);
        Assert.Equal("Theme", attr.Label);
        Assert.Equal("appearance", attr.Tab);
    }

    [Fact]
    public void SettingAttribute_WithExplicitControl_UsesProvided()
    {
        var attr = new SettingAttribute("Font Size", "terminal", "number", "Terminal font size in pixels");
        Assert.Equal("number", attr.Control);
        Assert.Equal("Terminal font size in pixels", attr.Description);
    }

    [Fact]
    public void ConfigSchemaGenerator_GeneratesEntryForEachKey()
    {
        var entries = ConfigSchemaGenerator.Generate();
        Assert.NotEmpty(entries);
        Assert.Contains(entries, e => e.Key == "theme");
        Assert.Contains(entries, e => e.Key == "terminal.fontSize");
        Assert.Contains(entries, e => e.Key == "terminal.fontFamily");
        Assert.Contains(entries, e => e.Key == "updates.checkOnLaunch");
        Assert.Contains(entries, e => e.Key == "diagnostics.enabled");
        Assert.DoesNotContain(entries, e => e.Key == "updates.channel");
        Assert.DoesNotContain(entries, e => e.Key == "telemetry.enabled");
        Assert.DoesNotContain(entries, e => e.Key == "terminal.fontLigatures");
    }

    [Fact]
    public void ConfigSchemaGenerator_DecoratedKeyCarriesLabel()
    {
        var entries = ConfigSchemaGenerator.Generate();
        var themeEntry = entries.First(e => e.Key == "theme");
        Assert.Equal("Theme", themeEntry.Label);
        Assert.Equal("appearance", themeEntry.Tab);
    }

    [Fact]
    public void ConfigSchemaGenerator_DecoratedKeyCarriesControl()
    {
        var entries = ConfigSchemaGenerator.Generate();
        var fontSizeEntry = entries.First(e => e.Key == "terminal.fontSize");
        Assert.Equal("number", fontSizeEntry.Control);
    }

    [Fact]
    public void ConfigSchemaGenerator_UndecoratedKeyGetsGenericFallback()
    {
        var entries = ConfigSchemaGenerator.Generate();
        var extraEntries = entries.Where(e => e.Key.StartsWith("extra."));
        foreach (var entry in extraEntries)
            Assert.Equal("text", entry.Control);
    }

    [Fact]
    public void ConfigSchemaGenerator_EveryEntryHasNonEmptyKey()
    {
        var entries = ConfigSchemaGenerator.Generate();
        foreach (var entry in entries)
            Assert.False(string.IsNullOrEmpty(entry.Key));
    }

    [Fact]
    public void ConfigReferenceDoc_GeneratesMarkdown()
    {
        var doc = ConfigSchemaGenerator.GenerateReferenceDoc();
        Assert.Contains("# Configuration Reference", doc);
        Assert.Contains("theme", doc);
        Assert.Contains("terminal.fontSize", doc);
        Assert.Contains("diagnostics.enabled", doc);
        Assert.DoesNotContain("telemetry.enabled", doc);
    }

    [Fact]
    public void ConfigReferenceDoc_GroupsByTab()
    {
        var doc = ConfigSchemaGenerator.GenerateReferenceDoc();
        Assert.Contains("## appearance", doc);
        Assert.Contains("## terminal", doc);
        Assert.Contains("## diagnostics", doc);
        Assert.DoesNotContain("## privacy", doc);
        Assert.DoesNotContain("## audio", doc);
    }

    [Fact]
    public void ConfigReferenceDoc_IncludesLabelAndDescription()
    {
        var doc = ConfigSchemaGenerator.GenerateReferenceDoc();
        Assert.Contains("Theme", doc);
    }
    [Fact]
    public void Schema_IncludesSurvivingConfigDomains()
    {
        var entries = ConfigSchemaGenerator.Generate();
        var tabs = entries.Select(e => e.Tab).Distinct().ToList();
        Assert.Contains("appearance", tabs);
        Assert.Contains("terminal", tabs);
        Assert.Contains("updates", tabs);
        Assert.Contains("diagnostics", tabs);
        Assert.Contains("bay", tabs);
        Assert.Contains("keyboard", tabs);
        Assert.Contains("tools", tabs);
    }

    [Fact]
    public void Schema_ExcludesRemovedAudioPrivacySections()
    {
        var entries = ConfigSchemaGenerator.Generate();
        var keys = entries.Select(e => e.Key).ToList();
        Assert.DoesNotContain(keys, k => k.StartsWith("telemetry.", System.StringComparison.Ordinal));
        Assert.DoesNotContain(keys, k => k.StartsWith("pushToTalk.", System.StringComparison.Ordinal));
        Assert.DoesNotContain(keys, k => k.StartsWith("speech.", System.StringComparison.Ordinal));
        Assert.DoesNotContain(keys, k => k.StartsWith("remoteConfig.", System.StringComparison.Ordinal));
        Assert.DoesNotContain(keys, k => k.StartsWith("lspServers.", System.StringComparison.Ordinal));
        Assert.DoesNotContain(keys, k => k.StartsWith("adapterCommands.", System.StringComparison.Ordinal));
        Assert.DoesNotContain(keys, k => k == "terminal.fontLigatures");
        Assert.DoesNotContain(keys, k => k.StartsWith("updates.autoInstall", System.StringComparison.Ordinal));
        Assert.DoesNotContain(keys, k => k.StartsWith("updates.channel", System.StringComparison.Ordinal));
    }


    [Fact]
    public void Schema_EveryEntryHasNonEmptyLabelAndTab()
    {
        var entries = ConfigSchemaGenerator.Generate();
        foreach (var entry in entries)
        {
            Assert.False(string.IsNullOrEmpty(entry.Label), $"entry {entry.Key} has empty label");
            Assert.False(string.IsNullOrEmpty(entry.Tab), $"entry {entry.Key} has empty tab");
            Assert.False(string.IsNullOrEmpty(entry.Control), $"entry {entry.Key} has empty control");
        }
    }
    [Fact]
    public void WriteReferenceDoc_WritesFileToDisk()
    {
        var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-config-ref-{System.Guid.NewGuid():N}.md");
        try
        {
            ConfigSchemaGenerator.WriteReferenceDoc(tempPath);
            var content = System.IO.File.ReadAllText(tempPath);
            Assert.Contains("# Configuration Reference", content);
            Assert.Contains("theme", content);
            Assert.Contains("## appearance", content);
        }
        finally
        {
            if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath);
        }
    }
}
