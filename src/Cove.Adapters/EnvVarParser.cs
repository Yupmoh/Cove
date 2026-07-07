using System.Text.RegularExpressions;

namespace Cove.Adapters;

using Cove.Protocol;


public sealed partial class EnvVarParser
{
    [GeneratedRegex(@"^(?:export\s+)?(?<key>[A-Za-z_][A-Za-z0-9_]*)=(?<value>.*)$", RegexOptions.None)]
    private static partial Regex AssignmentPattern();

    [GeneratedRegex(@"(?i).*(?:_KEY|_TOKEN|_SECRET|_PASSWORD)$", RegexOptions.None)]
    private static partial Regex SecretPattern();

    public static List<AdapterEnvVar> Parse(string text)
    {
        var entries = new List<AdapterEnvVar>();
        if (string.IsNullOrEmpty(text))
            return entries;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (string.IsNullOrEmpty(line))
                continue;
            if (line.StartsWith('#'))
                continue;

            var match = AssignmentPattern().Match(line);
            if (!match.Success)
                continue;

            var key = match.Groups["key"].Value;
            var value = match.Groups["value"].Value;

            if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
                value = value[1..^1];
            else if (value.Length >= 2 && value.StartsWith('\'') && value.EndsWith('\''))
                value = value[1..^1];

            entries.Add(new AdapterEnvVar(key, value, Enabled: true));
        }

        return entries;
    }

    public static string MaskSecret(string key, string value)
    {
        return SecretPattern().IsMatch(key) ? "****" : value;
    }
}
