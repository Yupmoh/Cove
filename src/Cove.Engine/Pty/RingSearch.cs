using System;
using System.Text;
using System.Text.RegularExpressions;
using Cove.Protocol;

namespace Cove.Engine.Pty;

public static class RingSearch
{
    private static readonly Regex AnsiRegex = new(
        "\u001b\\[[0-9;?]*[ -/]*[@-~]|\u001b\\][^\u0007\u001b]*(?:\u0007|\u001b\\\\)|\u001b[@-Z\\\\-_]",
        RegexOptions.Compiled);

    public static SearchMatch[] Find(ReadOnlySpan<byte> content, string query, bool caseSensitive)
    {
        if (string.IsNullOrEmpty(query))
            return Array.Empty<SearchMatch>();

        var text = Encoding.UTF8.GetString(content);
        text = AnsiRegex.Replace(text, string.Empty);

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var matches = new System.Collections.Generic.List<SearchMatch>();
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length > 0 && line[line.Length - 1] == '\r')
                line = line[..^1];
            if (line.Contains(query, comparison))
                matches.Add(new SearchMatch(i, line));
        }
        return matches.ToArray();
    }
}
