using System.Text;
using System.Text.RegularExpressions;

namespace Cove.Adapters;

public static partial class AdapterIconSanitizer
{
    private const int MaxBytes = 64 * 1024;

    [GeneratedRegex(@"\son[a-zA-Z]+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase)]
    private static partial Regex OnHandlerRegex();

    [GeneratedRegex(@"\s(?:xlink:)?href\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalRefRegex();

    [GeneratedRegex(@"<svg\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex OpenSvgRegex();

    [GeneratedRegex(@"\s(width|height)\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase)]
    private static partial Regex RootDimensionRegex();

    public static string? Sanitize(string? svg)
    {
        if (string.IsNullOrWhiteSpace(svg))
            return null;
        if (Encoding.UTF8.GetByteCount(svg) > MaxBytes)
            return null;

        var trimmed = svg.Trim();
        var lower = trimmed.ToLowerInvariant();
        if (!lower.Contains("<svg"))
            return null;
        if (lower.Contains("<script") || lower.Contains("<foreignobject") || lower.Contains("javascript:") || lower.Contains("<!entity") || lower.Contains("<iframe"))
            return null;

        var cleaned = OnHandlerRegex().Replace(trimmed, "");
        cleaned = ExternalRefRegex().Replace(cleaned, "");

        var open = OpenSvgRegex().Match(cleaned);
        if (open.Success)
        {
            var strippedTag = RootDimensionRegex().Replace(open.Value, "");
            cleaned = cleaned[..open.Index] + strippedTag + cleaned[(open.Index + open.Length)..];
        }

        return cleaned;
    }
}

public static class RetentionThreshold
{
    public static bool IsHidden(string? currentValue, string? recommended)
    {
        if (string.IsNullOrEmpty(recommended))
            return false;
        if (TryNumber(currentValue, out var current) && TryNumber(recommended, out var threshold))
            return current >= threshold;
        return false;
    }

    private static bool TryNumber(string? value, out double number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var match = Regex.Match(value, @"-?\d+(\.\d+)?");
        return match.Success && double.TryParse(match.Value, System.Globalization.CultureInfo.InvariantCulture, out number);
    }
}
