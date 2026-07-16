using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Cove.Adapters;

namespace Cove.Engine.Agents;

public static class ScreenStateDetector
{
    private static readonly ConcurrentDictionary<string, Regex> PatternCache = new();
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    public static string AnsiStrip(ReadOnlySpan<byte> raw)
    {
        var kept = new byte[raw.Length];
        var n = 0;
        var i = 0;
        while (i < raw.Length)
        {
            var b = raw[i];
            if (b == 0x1b)
            {
                i++;
                if (i >= raw.Length)
                    break;
                var kind = raw[i];
                i++;
                if (kind == (byte)'[')
                {
                    while (i < raw.Length && raw[i] >= 0x20 && raw[i] <= 0x3f)
                        i++;
                    if (i < raw.Length)
                        i++;
                }
                else if (kind is (byte)']' or (byte)'P' or (byte)'_' or (byte)'^' or (byte)'X')
                {
                    while (i < raw.Length)
                    {
                        if (raw[i] == 0x07)
                        {
                            i++;
                            break;
                        }
                        if (raw[i] == 0x1b && i + 1 < raw.Length && raw[i + 1] == (byte)'\\')
                        {
                            i += 2;
                            break;
                        }
                        i++;
                    }
                }
                continue;
            }
            if (b < 0x20 && b != (byte)'\n' && b != (byte)'\t')
            {
                i++;
                continue;
            }
            kept[n++] = b;
            i++;
        }
        return Encoding.UTF8.GetString(kept, 0, n);
    }

    public static string? Evaluate(ScreenStateDeclaration declaration, string strippedTail, bool ringAdvanced, bool quietElapsed, string currentStatus)
    {
        foreach (var rule in declaration.Rules)
        {
            var regex = PatternCache.GetOrAdd(rule.Pattern,
                static p => new Regex(p, RegexOptions.Multiline | RegexOptions.CultureInvariant, MatchTimeout));
            bool matched;
            try
            {
                matched = regex.IsMatch(strippedTail);
            }
            catch (RegexMatchTimeoutException)
            {
                continue;
            }
            if (matched)
                return rule.Status == currentStatus ? null : rule.Status;
        }
        if (ringAdvanced)
            return currentStatus == "active" ? null : "active";
        if (quietElapsed)
            return currentStatus == "idle" ? null : "idle";
        return null;
    }
}
