using System.Text.RegularExpressions;

namespace Cove.Engine.Pty;

public sealed record TerminalLink(LinkKind Kind, string Text, int StartIndex, int Length, string? FilePath, int? Line, int? Column);

public enum LinkKind { FilePath, Url, TaskRef }

public sealed class TerminalLinkDetector
{
    private static readonly Regex UrlPattern = new(
        @"https?://[^\s\x1b""<>'\),;]+",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));
    private static readonly Regex FilePathPattern = new(
        @"(?:(?<=^|\s)(/[A-Za-z0-9._\-/]+|~/[A-Za-z0-9._\-/]+|[A-Za-z]:[\\/][A-Za-z0-9._\-\\/]+|[A-Za-z0-9_][A-Za-z0-9._\-]*\.[A-Za-z]{1,5}))(?::(\d+))?(?::(\d+))?",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly Regex TaskRefPattern = new(
        @"(?<=^|\s)([A-Z]{2,5}-\d{1,6})\b",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    public IReadOnlyList<TerminalLink> Detect(string text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var links = new System.Collections.Generic.List<TerminalLink>();
        var occupied = new System.Collections.Generic.List<(int Start, int End)>();

        foreach (Match m in UrlPattern.Matches(text))
        {
            if (Overlaps(occupied, m.Index, m.Length))
                continue;
            var urlText = m.Value.TrimEnd('.', ',', ';', ')');
            links.Add(new TerminalLink(LinkKind.Url, urlText, m.Index, urlText.Length, null, null, null));
            occupied.Add((m.Index, m.Index + urlText.Length));
        }

        foreach (Match m in FilePathPattern.Matches(text))
        {
            if (Overlaps(occupied, m.Index, m.Length))
                continue;

            var pathGroup = m.Groups[1].Value;
            var pathStart = m.Index + m.Value.IndexOf(pathGroup);

            int? line = m.Groups[2].Success && int.TryParse(m.Groups[2].Value, out var l) ? l : null;
            int? col = m.Groups[3].Success && int.TryParse(m.Groups[3].Value, out var c) ? c : null;

            links.Add(new TerminalLink(LinkKind.FilePath, pathGroup, pathStart, pathGroup.Length, pathGroup, line, col));
            occupied.Add((pathStart, pathStart + pathGroup.Length));
        }

        foreach (Match m in TaskRefPattern.Matches(text))
        {
            var refText = m.Groups[1].Value;
            var refStart = m.Index + m.Value.IndexOf(refText);
            if (Overlaps(occupied, refStart, refText.Length))
                continue;
            links.Add(new TerminalLink(LinkKind.TaskRef, refText, refStart, refText.Length, null, null, null));
            occupied.Add((refStart, refStart + refText.Length));
        }

        links.Sort((a, b) => a.StartIndex.CompareTo(b.StartIndex));
        return links;
    }

    private static bool Overlaps(System.Collections.Generic.List<(int Start, int End)> occupied, int start, int length)
    {
        var end = start + length;
        foreach (var (s, e) in occupied)
        {
            if (start < e && end > s)
                return true;
        }
        return false;
    }
}
