using System.Text.Json.Serialization;

namespace Cove.Engine.Tui;

public sealed record TerminalCapabilities(
    string TerminalType,
    int ColorDepth,
    bool SupportsTrueColor,
    bool SupportsUnicode,
    bool SupportsEmoji,
    bool SupportsBrackets,
    bool NoColor,
    string GlyphSet)
{
    public static TerminalCapabilities Detect(string? term, string? colorterm, string? noColor, string? termProgram)
    {
        var termLower = (term ?? "").ToLowerInvariant();
        var programLower = (termProgram ?? "").ToLowerInvariant();
        var noColorSet = !string.IsNullOrEmpty(noColor);

        var (colorDepth, supportsTrueColor) = DetectColor(termLower, colorterm, noColorSet);
        var supportsUnicode = CanUnicode(termLower, programLower);
        var supportsEmoji = supportsUnicode && CanEmoji(programLower);
        var supportsBrackets = CanBrackets(termLower, programLower);
        var glyphSet = ChooseGlyphSet(supportsUnicode, supportsEmoji, noColorSet);

        return new TerminalCapabilities(
            termLower,
            colorDepth,
            supportsTrueColor,
            supportsUnicode,
            supportsEmoji,
            supportsBrackets,
            noColorSet,
            glyphSet);
    }

    private static (int depth, bool trueColor) DetectColor(string term, string? colorterm, bool noColor)
    {
        if (noColor) return (0, false);
        if (!string.IsNullOrEmpty(colorterm))
        {
            var ct = colorterm.ToLowerInvariant();
            if (ct == "truecolor" || ct == "24bit") return (24, true);
        }
        if (term.Contains("256color") || term.Contains("xterm-256")) return (256, false);
        if (term.Contains("xterm") || term.Contains("screen") || term.Contains("tmux")) return (16, false);
        if (term.Contains("dumb")) return (0, false);
        return (8, false);
    }

    private static bool CanUnicode(string term, string program)
    {
        if (term.Contains("linux")) return false;
        if (program.Contains("iterm") || program.Contains("wezterm") || program.Contains("ghostty")) return true;
        if (program.Contains("apple") || program.Contains("terminal")) return true;
        if (term.Contains("xterm") || term.Contains("screen") || term.Contains("tmux")) return true;
        return true;
    }

    private static bool CanEmoji(string program)
    {
        return program.Contains("iterm") || program.Contains("ghostty") || program.Contains("wezterm") || program.Contains("kitty");
    }

    private static bool CanBrackets(string term, string program)
    {
        if (term.Contains("dumb")) return false;
        if (term.Contains("linux")) return false;
        return true;
    }

    private static string ChooseGlyphSet(bool unicode, bool emoji, bool noColor)
    {
        if (!unicode) return "ascii";
        if (emoji) return "nerd-emoji";
        return "nerd";
    }

    public string DegradeColor(string color)
    {
        if (NoColor) return "";
        return color;
    }

    public string DegradeGlyph(string rich, string fallback)
    {
        if (!SupportsUnicode) return fallback;
        return rich;
    }
}
