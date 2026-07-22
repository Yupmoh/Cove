using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZLogger;

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
    private readonly ILogger _logger;

    public ThemeService(string dataDir)
        : this(dataDir, NullLogger.Instance)
    {
    }

    public ThemeService(string dataDir, ILogger? logger)
    {
        _logger = logger ?? NullLogger.Instance;
        _themesDir = System.IO.Path.Combine(dataDir, "themes");
        System.IO.Directory.CreateDirectory(_themesDir);
        _builtins = [
            new Theme("catppuccin-mocha", "dark", "#1e1e2e", "#cdd6f4", "#181825", "#cdd6f4", "#cba6f7",
                ["#45475a", "#f38ba8", "#a6e3a1", "#f9e2af", "#89b4fa", "#f5c2e7", "#94e2d5", "#bac2de",
                 "#585b70", "#f38ba8", "#a6e3a1", "#f9e2af", "#89b4fa", "#f5c2e7", "#94e2d5", "#a6adc8"])
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
        if (!TryResolveThemePath(theme.Name, out var path))
            throw new System.ArgumentException("theme name must be a safe path segment", nameof(theme));
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
        if (!TryResolveThemePath(name, out var path))
            return false;
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
            if (!PathContainment.IsContainedPhysical(_themesDir, file))
            {
                _logger.ThemeFileOutsideRoot(file);
                continue;
            }
            try
            {
                var json = System.IO.File.ReadAllText(file);
                var theme = JsonSerializer.Deserialize(json, ThemeJsonContext.Default.Theme);
                if (theme is null)
                {
                    _logger.ThemeMissingData(file);
                    continue;
                }
                if (!PathContainment.IsSafeSegment(theme.Name))
                {
                    _logger.ThemeUnsafeEmbeddedName(theme.Name, file);
                    continue;
                }
                _custom.Add(theme);
            }
            catch (System.Exception ex)
            {
                _logger.ThemeLoadFailed(file, ex.Message);
            }
        }
    }

    private bool TryResolveThemePath(string name, out string path)
    {
        path = string.Empty;
        var fileName = $"{name}.json";
        if (!PathContainment.IsSafeSegment(name)
            || !PathContainment.TryResolveContained(_themesDir, out _, out path, fileName)
            || !PathContainment.IsContainedPhysical(_themesDir, path))
        {
            _logger.ThemeUnsafeCustomName(name);
            return false;
        }
        return true;
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

internal static partial class ThemeServiceLog
{
    [ZLoggerMessage(LogLevel.Warning, "themes skipped custom theme file outside root path={path}")]
    public static partial void ThemeFileOutsideRoot(this ILogger logger, string path);

    [ZLoggerMessage(LogLevel.Warning, "themes skipped custom theme with missing data path={path}")]
    public static partial void ThemeMissingData(this ILogger logger, string path);

    [ZLoggerMessage(LogLevel.Warning, "themes skipped custom theme with unsafe embedded name name={name} path={path}")]
    public static partial void ThemeUnsafeEmbeddedName(this ILogger logger, string name, string path);

    [ZLoggerMessage(LogLevel.Warning, "themes failed to load custom theme path={path} error={error}")]
    public static partial void ThemeLoadFailed(this ILogger logger, string path, string error);

    [ZLoggerMessage(LogLevel.Warning, "themes rejected unsafe custom theme name={name}")]
    public static partial void ThemeUnsafeCustomName(this ILogger logger, string name);
}
