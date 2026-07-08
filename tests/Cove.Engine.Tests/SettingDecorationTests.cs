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
        Assert.Contains(entries, e => e.Key == "updates.channel");
        Assert.Contains(entries, e => e.Key == "telemetry.enabled");
        Assert.Contains(entries, e => e.Key == "diagnostics.enabled");
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
        Assert.Contains("telemetry.enabled", doc);
    }

    [Fact]
    public void ConfigReferenceDoc_GroupsByTab()
    {
        var doc = ConfigSchemaGenerator.GenerateReferenceDoc();
        Assert.Contains("## appearance", doc);
        Assert.Contains("## terminal", doc);
        Assert.Contains("## privacy", doc);
    }

    [Fact]
    public void ConfigReferenceDoc_IncludesLabelAndDescription()
    {
        var doc = ConfigSchemaGenerator.GenerateReferenceDoc();
        Assert.Contains("Theme", doc);
    }
}
