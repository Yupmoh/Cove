using System.Text.RegularExpressions;

namespace Cove.Adapters;

public sealed record SigilMatch(string Sigil, string Name, string? Scope, SkillEntry? Skill, string? Error);

public sealed partial class SigilResolver
{
    [GeneratedRegex(@"(?:^|\s)\+(?<name>[a-zA-Z0-9][a-zA-Z0-9-]*)(?:@(?<scope>[a-zA-Z0-9-]+))?", RegexOptions.None)]
    private static partial Regex SigilPattern();

    private readonly SkillIndex _index;

    public SigilResolver(SkillIndex index)
    {
        _index = index;
    }

    public List<SigilMatch> Scan(string text)
    {
        var matches = new List<SigilMatch>();
        if (string.IsNullOrEmpty(text))
            return matches;

        var pattern = SigilPattern();
        var lines = text.Split('\n');
        var inFence = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }
            if (inFence)
                continue;

            if (trimmed.StartsWith('`') && trimmed.EndsWith('`') && trimmed.Length > 1)
                continue;

            foreach (Match m in pattern.Matches(line))
            {
                var name = m.Groups["name"].Value;
                var scope = m.Groups["scope"].Success ? m.Groups["scope"].Value : null;
                var skill = scope is not null
                    ? ResolveScoped(name, scope)
                    : _index.Resolve(name);
                matches.Add(new SigilMatch(m.Value.Trim(), name, scope, skill, skill is null ? $"unresolved: +{name}" : null));
            }
        }

        return matches;
    }

    private SkillEntry? ResolveScoped(string name, string scope)
    {
        var (source, adapter) = ParseScope(scope);
        if (source is null)
            return null;
        return _index.ResolveInScope(name, source.Value, adapter);
    }

    private static (SkillSource? source, string? adapter) ParseScope(string scope)
    {
        var s = scope.ToLowerInvariant();
        return s switch
        {
            "cove-user" => (SkillSource.CoveUser, null),
            "cove-project" => (SkillSource.CoveProject, null),
            "vercel-labs-skills" => (SkillSource.VercelLabs, null),
            _ when s.StartsWith("harness-project-", StringComparison.Ordinal) => (SkillSource.Harness, s["harness-project-".Length..]),
            _ when s.StartsWith("harness-", StringComparison.Ordinal) => (SkillSource.Harness, s["harness-".Length..]),
            _ => (null, null),
        };
    }
}
