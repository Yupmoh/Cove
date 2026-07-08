using Cove.Engine.Tui;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class TerminalCapabilitiesTests
{
    [Fact]
    public void Detect_Iterm2_TrueColorAndEmoji()
    {
        var caps = TerminalCapabilities.Detect("xterm-256color", "truecolor", null, "iTerm.app");
        Assert.True(caps.SupportsTrueColor);
        Assert.Equal(24, caps.ColorDepth);
        Assert.True(caps.SupportsUnicode);
        Assert.True(caps.SupportsEmoji);
        Assert.True(caps.SupportsBrackets);
    }

    [Fact]
    public void Detect_Ghostty_TrueColorAndEmoji()
    {
        var caps = TerminalCapabilities.Detect("xterm-ghostty", "truecolor", null, "ghostty");
        Assert.True(caps.SupportsTrueColor);
        Assert.True(caps.SupportsEmoji);
    }
    [Fact]
    public void Detect_WezTerm_TrueColorAndEmoji()
    {
        var caps = TerminalCapabilities.Detect("xterm-256color", "truecolor", null, "WezTerm");
        Assert.True(caps.SupportsTrueColor);
        Assert.True(caps.SupportsEmoji);
        Assert.True(caps.SupportsUnicode);
    }

    [Fact]
    public void Detect_NoColor_DropsToZeroColor()
    {
        var caps = TerminalCapabilities.Detect("xterm-256color", "truecolor", "1", "iTerm.app");
        Assert.True(caps.NoColor);
        Assert.Equal(0, caps.ColorDepth);
        Assert.False(caps.SupportsTrueColor);
    }

    [Fact]
    public void Detect_NoColorEnvAnyValue_DropsColor()
    {
        var caps = TerminalCapabilities.Detect("xterm-256color", null, "anything", null);
        Assert.True(caps.NoColor);
        Assert.Equal(0, caps.ColorDepth);
    }

    [Fact]
    public void Detect_256ColorWithoutTrueColor()
    {
        var caps = TerminalCapabilities.Detect("xterm-256color", null, null, null);
        Assert.Equal(256, caps.ColorDepth);
        Assert.False(caps.SupportsTrueColor);
    }

    [Fact]
    public void Detect_BasicXterm_16Color()
    {
        var caps = TerminalCapabilities.Detect("xterm", null, null, null);
        Assert.Equal(16, caps.ColorDepth);
    }

    [Fact]
    public void Detect_DumbTerminal_NoColorNoBrackets()
    {
        var caps = TerminalCapabilities.Detect("dumb", null, null, null);
        Assert.Equal(0, caps.ColorDepth);
        Assert.False(caps.SupportsBrackets);
    }

    [Fact]
    public void Detect_LinuxConsole_NoUnicode()
    {
        var caps = TerminalCapabilities.Detect("linux", null, null, null);
        Assert.False(caps.SupportsUnicode);
    }

    [Fact]
    public void Detect_GlyphSet_NerdEmojiForModernTerminal()
    {
        var caps = TerminalCapabilities.Detect("xterm-256color", "truecolor", null, "iTerm.app");
        Assert.Equal("nerd-emoji", caps.GlyphSet);
    }

    [Fact]
    public void Detect_GlyphSet_NerdForUnicodeNoEmoji()
    {
        var caps = TerminalCapabilities.Detect("xterm-256color", null, null, null);
        Assert.Equal("nerd", caps.GlyphSet);
    }

    [Fact]
    public void Detect_GlyphSet_AsciiForNonUnicode()
    {
        var caps = TerminalCapabilities.Detect("linux", null, null, null);
        Assert.Equal("ascii", caps.GlyphSet);
    }

    [Fact]
    public void DegradeColor_ReturnsEmptyWhenNoColor()
    {
        var caps = TerminalCapabilities.Detect("xterm-256color", null, "1", null);
        Assert.Equal("", caps.DegradeColor("#ff0000"));
    }

    [Fact]
    public void DegradeColor_ReturnsColorWhenColorEnabled()
    {
        var caps = TerminalCapabilities.Detect("xterm-256color", null, null, null);
        Assert.Equal("#ff0000", caps.DegradeColor("#ff0000"));
    }

    [Fact]
    public void DegradeGlyph_ReturnsFallbackForNonUnicode()
    {
        var caps = TerminalCapabilities.Detect("linux", null, null, null);
        Assert.Equal(">", caps.DegradeGlyph("▶", ">"));
    }

    [Fact]
    public void DegradeGlyph_ReturnsRichForUnicode()
    {
        var caps = TerminalCapabilities.Detect("xterm-256color", null, null, null);
        Assert.Equal("▶", caps.DegradeGlyph("▶", ">"));
    }

    [Fact]
    public void Detect_Tmux_16Color()
    {
        var caps = TerminalCapabilities.Detect("tmux-256color", null, null, "tmux");
        Assert.Equal(256, caps.ColorDepth);
        Assert.True(caps.SupportsUnicode);
    }
}
