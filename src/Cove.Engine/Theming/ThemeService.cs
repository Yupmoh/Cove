using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cove.Engine.Theming;

public sealed record ThemeColor(string Hex);

public sealed record Theme(
    string Name,
    string Type,
    string TerminalBackground,
    string TerminalForeground,
    string ChromeSurface,
    string ChromeText,
    string ChromeAccent,
    string[]? Ansi = null);

public sealed class ThemeService
{
    private Theme? _active;
    private readonly List<Theme> _builtins;
    private readonly List<Theme> _custom = [];
    private readonly string _themesDir;

    public ThemeService(string dataDir)
    {
        _themesDir = System.IO.Path.Combine(dataDir, "themes");
        System.IO.Directory.CreateDirectory(_themesDir);
        _builtins = [
            new Theme("catppuccin-mocha", "dark", "#1e1e2e", "#cdd6f4", "#181825", "#cdd6f4", "#cba6f7",
                ["#45475a", "#f38ba8", "#a6e3a1", "#f9e2af", "#89b4fa", "#f5c2e7", "#94e2d5", "#bac2de",
                 "#585b70", "#f38ba8", "#a6e3a1", "#f9e2af", "#89b4fa", "#f5c2e7", "#94e2d5", "#a6adc8"]),
            new Theme("cove-harbor", "dark", "#0b1622", "#e5e9f0", "#0b1622", "#e5e9f0", "#4a9eff",
                ["#3b4252", "#bf616a", "#a3be8c", "#ebcb8b", "#81a1c1", "#b48ead", "#88c0d0", "#e5e9f0",
                 "#4c566a", "#bf616a", "#a3be8c", "#ebcb8b", "#81a1c1", "#b48ead", "#8fbcbb", "#eceff4"]),
            new Theme("cove-daybreak", "light", "#ffffff", "#1a1a2e", "#ffffff", "#1a1a2e", "#2563eb",
                ["#24292f", "#cf222e", "#116329", "#4d2d00", "#0969da", "#8250df", "#1b7c83", "#6e7781",
                 "#57606a", "#a40e26", "#1a7f37", "#633c01", "#218bff", "#a475f9", "#3192aa", "#8c959f"]),
            new Theme("cove-midnight", "dark", "#0a0a1a", "#c0c0d0", "#0a0a1a", "#c0c0d0", "#7c3aed",
                ["#2a2a40", "#f2779c", "#8ee8b4", "#eed49f", "#8aadf4", "#c6a0f6", "#8bd5ca", "#b8c0e0",
                 "#494960", "#f2779c", "#8ee8b4", "#eed49f", "#8aadf4", "#c6a0f6", "#8bd5ca", "#cad3f5"]),
            new Theme("cove-shoal", "light", "#f5f0e8", "#2d2418", "#f5f0e8", "#2d2418", "#b45309",
                ["#3c3836", "#9d0006", "#79740e", "#b57614", "#076678", "#8f3f71", "#427b58", "#7c6f64",
                 "#928374", "#9d0006", "#79740e", "#b57614", "#076678", "#8f3f71", "#427b58", "#3c3836"]),
            new Theme("cove-beacon", "dark", "#0d1117", "#f0f0f0", "#0d1117", "#f0f0f0", "#58a6ff",
                ["#484f58", "#ff7b72", "#3fb950", "#d29922", "#58a6ff", "#bc8cff", "#39c5cf", "#b1bac4",
                 "#6e7681", "#ffa198", "#56d364", "#e3b341", "#79c0ff", "#d2a8ff", "#56d4dd", "#f0f6fc"]),
            new Theme("cove-chalk", "light", "#fafafa", "#1a1a1a", "#fafafa", "#1a1a1a", "#0969da",
                ["#24292f", "#cf222e", "#116329", "#4d2d00", "#0969da", "#8250df", "#1b7c83", "#6e7781",
                 "#57606a", "#a40e26", "#1a7f37", "#633c01", "#218bff", "#a475f9", "#3192aa", "#8c959f"]),
        ];
        LoadCustomThemes();
    }

    public IReadOnlyList<Theme> ListAll()
    {
        var all = new List<Theme>(_builtins);
        all.AddRange(_custom);
        return all;
    }

    public IReadOnlyList<Theme> ListBuiltins() => _builtins.AsReadOnly();

    public IReadOnlyList<Theme> ListCustom() => _custom.AsReadOnly();

    public Theme? Get(string name) => ListAll().FirstOrDefault(t => t.Name == name);

    public Theme? GetActive() => _active;

    public Theme SetActive(string name)
    {
        var theme = Get(name) ?? throw new System.ArgumentException($"theme '{name}' not found");
        _active = theme;
        return theme;
    }

    public Theme? SetActiveIfKnown(string name)
    {
        var theme = Get(name) ?? Get("catppuccin-mocha");
        if (theme is null) return null;
        _active = theme;
        return theme;
    }

    public Theme LoadFromJson(string json)
    {
        var theme = JsonSerializer.Deserialize(json, ThemeJsonContext.Default.Theme)
            ?? throw new System.ArgumentException("invalid theme JSON");
        ValidateTheme(theme);
        return theme;
    }

    public void SaveCustom(Theme theme)
    {
        ValidateTheme(theme);
        var path = System.IO.Path.Combine(_themesDir, $"{theme.Name}.json");
        var json = JsonSerializer.Serialize(theme, ThemeJsonContext.Default.Theme);
        System.IO.File.WriteAllText(path, json);
        if (!_custom.Any(t => t.Name == theme.Name))
            _custom.Add(theme);
        else
        {
            var idx = _custom.FindIndex(t => t.Name == theme.Name);
            _custom[idx] = theme;
        }
    }

    public bool DeleteCustom(string name)
    {
        if (_builtins.Any(t => t.Name == name))
            return false;
        var path = System.IO.Path.Combine(_themesDir, $"{name}.json");
        if (System.IO.File.Exists(path))
            System.IO.File.Delete(path);
        return _custom.RemoveAll(t => t.Name == name) > 0;
    }

    public bool IsBuiltin(string name) => _builtins.Any(t => t.Name == name);

    private void LoadCustomThemes()
    {
        if (!System.IO.Directory.Exists(_themesDir)) return;
        foreach (var file in System.IO.Directory.EnumerateFiles(_themesDir, "*.json"))
        {
            try
            {
                var json = System.IO.File.ReadAllText(file);
                var theme = JsonSerializer.Deserialize(json, ThemeJsonContext.Default.Theme);
                if (theme is not null) _custom.Add(theme);
            }
            catch { }
        }
    }

    private static void ValidateTheme(Theme theme)
    {
        if (string.IsNullOrWhiteSpace(theme.Name))
            throw new System.ArgumentException("theme name is required");
        if (theme.Type is not ("dark" or "light"))
            throw new System.ArgumentException("theme type must be 'dark' or 'light'");
        if (!IsValidHex(theme.TerminalBackground))
            throw new System.ArgumentException("terminal.background must be a valid hex color");
        if (!IsValidHex(theme.TerminalForeground))
            throw new System.ArgumentException("terminal.foreground must be a valid hex color");
        if (!IsValidHex(theme.ChromeSurface))
            throw new System.ArgumentException("chrome.surface must be a valid hex color");
        if (!IsValidHex(theme.ChromeText))
            throw new System.ArgumentException("chrome.text must be a valid hex color");
        if (!IsValidHex(theme.ChromeAccent))
            throw new System.ArgumentException("chrome.accent must be a valid hex color");
        if (theme.Ansi is { } ansi)
        {
            if (ansi.Length != 16)
                throw new System.ArgumentException("ansi palette must contain exactly 16 colors");
            foreach (var hex in ansi)
                if (!IsValidHex(hex))
                    throw new System.ArgumentException("ansi palette entries must be valid hex colors");
        }
    }

    private static bool IsValidHex(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return false;
        if (!hex.StartsWith('#')) return false;
        return hex.Length is 7 or 9;
    }
}

public static class ContrastValidator
{
    public static double ComputeContrastRatio(string hex1, string hex2)
    {
        var l1 = GetRelativeLuminance(hex1);
        var l2 = GetRelativeLuminance(hex2);
        var lighter = System.Math.Max(l1, l2);
        var darker = System.Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    public static bool MeetsAA(double ratio) => ratio >= 4.5;

    public static bool MeetsAAA(double ratio) => ratio >= 7.0;

    public static bool MeetsAALarge(double ratio) => ratio >= 3.0;

    private static double GetRelativeLuminance(string hex)
    {
        var (r, g, b) = ParseHex(hex);
        var rs = ToSrgb(r);
        var gs = ToSrgb(g);
        var bs = ToSrgb(b);
        return 0.2126 * rs + 0.7152 * gs + 0.0722 * bs;
    }

    private static double ToSrgb(int component)
    {
        var c = component / 255.0;
        return c <= 0.03928 ? c / 12.92 : System.Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static (int r, int g, int b) ParseHex(string hex)
    {
        var h = hex.TrimStart('#');
        if (h.Length == 8) h = h[..6];
        var r = int.Parse(h[0..2], System.Globalization.NumberStyles.HexNumber);
        var g = int.Parse(h[2..4], System.Globalization.NumberStyles.HexNumber);
        var b = int.Parse(h[4..6], System.Globalization.NumberStyles.HexNumber);
        return (r, g, b);
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Theme))]
public sealed partial class ThemeJsonContext : JsonSerializerContext { }
