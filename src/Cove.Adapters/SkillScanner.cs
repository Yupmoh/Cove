using System.IO;
using System.Text.Json.Serialization;

namespace Cove.Adapters;

public enum SkillSource
{
    CoveUser,
    CoveProject,
    Harness,
    VercelLabs,
}

public sealed record SkillEntry(
    string Name,
    string Description,
    string Path,
    SkillSource Source,
    string Provenance,
    string? AdapterName,
    string? Body);

public sealed class SkillScanner
{
    private const int MaxDepth = 2;
    private const int MaxDescriptionLength = 1024;

    public List<SkillEntry> ScanRoot(string root, SkillSource source, string? adapterName = null)
    {
        var skills = new List<SkillEntry>();
        if (!Directory.Exists(root))
            return skills;

        var provenance = source switch
        {
            SkillSource.Harness => adapterName is not null ? $"harness:{adapterName}" : "harness",
            SkillSource.CoveUser => "cove-user",
            SkillSource.CoveProject => "cove-project",
            SkillSource.VercelLabs => "vercel-labs-skills",
            _ => source.ToString().ToLowerInvariant(),
        };

        ScanDirectory(root, source, adapterName, provenance, skills, depth: 0);
        return skills;
    }

    private void ScanDirectory(string dir, SkillSource source, string? adapterName, string provenance, List<SkillEntry> skills, int depth)
    {
        if (depth > MaxDepth)
            return;

        var skillPath = Path.Combine(dir, "SKILL.md");
        if (File.Exists(skillPath))
        {
            var entry = ParseSkill(skillPath, dir, source, adapterName, provenance);
            if (entry is not null)
                skills.Add(entry);
            return;
        }

        try
        {
            foreach (var subdir in Directory.EnumerateDirectories(dir))
            {
                ScanDirectory(subdir, source, adapterName, provenance, skills, depth + 1);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (DirectoryNotFoundException) { }
    }

    private static SkillEntry? ParseSkill(string skillPath, string dir, SkillSource source, string? adapterName, string provenance)
    {
        try
        {
            var content = File.ReadAllText(skillPath);
            var (name, description, body) = ParseFrontmatter(content, dir);
            if (name is null || description is null)
                return null;
            if (description.Length > MaxDescriptionLength)
                return null;
            return new SkillEntry(name, description, skillPath, source, provenance, adapterName, body);
        }
        catch (IOException) { return null; }
    }

    private static (string? name, string? description, string? body) ParseFrontmatter(string content, string dir)
    {
        if (!content.StartsWith("---", StringComparison.Ordinal))
            return (null, null, null);

        var end = content.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0)
            return (null, null, null);

        var frontmatter = content.AsSpan(3, end - 3);
        var body = content.AsSpan(end + 4).Trim().ToString();

        string? name = null;
        string? description = null;

        foreach (var line in frontmatter.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.TrimEnd('\r');
            var colon = trimmed.IndexOf(':');
            if (colon < 0) continue;
            var key = trimmed.AsSpan(0, colon).Trim().ToString();
            var value = trimmed.AsSpan(colon + 1).Trim().Trim('"').Trim('\'').ToString();

            if (key == "name") name = value;
            else if (key == "description") description = value;
        }

        if (string.IsNullOrEmpty(name))
            name = Path.GetFileName(dir);

        return (name, description, body);
    }
}

public sealed class SkillIndex
{
    private readonly List<(string root, SkillSource source, string? adapterName)> _roots = new();
    private Dictionary<string, SkillEntry> _byName = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<(string name, SkillSource source, string? adapter), SkillEntry> _byScope = new(new ScopeKeyComparer());
    private List<SkillEntry> _all = new();

    public void AddRoot(string root, SkillSource source, string? adapterName = null)
    {
        _roots.Add((root, source, adapterName));
    }

    public IReadOnlyList<(string root, SkillSource source, string? adapterName)> GetRoots() => _roots;
    public void Rebuild()
    {
        var byScope = new Dictionary<(string, SkillSource, string?), SkillEntry>(new ScopeKeyComparer());
        var byName = new Dictionary<string, SkillEntry>(StringComparer.OrdinalIgnoreCase);
        var all = new List<SkillEntry>();
        var scanner = new SkillScanner();
        foreach (var (root, source, adapterName) in _roots)
        {
            var skills = scanner.ScanRoot(root, source, adapterName);
            foreach (var skill in skills)
            {
                if (!byName.ContainsKey(skill.Name))
                {
                    byName[skill.Name] = skill;
                    all.Add(skill);
                }
                byScope[(skill.Name, skill.Source, skill.AdapterName)] = skill;
            }
        }
        Volatile.Write(ref _byName, byName);
        Volatile.Write(ref _byScope, byScope);
        Volatile.Write(ref _all, all);
    }

    public IReadOnlyList<SkillEntry> List() => Volatile.Read(ref _all);

    public IReadOnlyList<SkillEntry> Search(string query)
    {
        var q = query.ToLowerInvariant();
        var snapshot = Volatile.Read(ref _all);
        return snapshot.Where(s => s.Name.ToLowerInvariant().Contains(q) || s.Description.ToLowerInvariant().Contains(q)).ToList();
    }
    public SkillEntry? Resolve(string name)
    {
        var byName = Volatile.Read(ref _byName);
        return byName.TryGetValue(name, out var skill) ? skill : null;
    }

    public SkillEntry? ResolveInScope(string name, SkillSource source, string? adapterName = null)
    {
        var byScope = Volatile.Read(ref _byScope);
        return byScope.TryGetValue((name, source, adapterName), out var skill) ? skill : null;
    }

    internal sealed class ScopeKeyComparer : IEqualityComparer<(string name, SkillSource source, string? adapter)>
    {
        public bool Equals((string name, SkillSource source, string? adapter) x, (string name, SkillSource source, string? adapter) y)
            => string.Equals(x.name, y.name, StringComparison.OrdinalIgnoreCase) && x.source == y.source && string.Equals(x.adapter, y.adapter, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string name, SkillSource source, string? adapter) obj)
            => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.name) ^ obj.source.GetHashCode() ^ (obj.adapter is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(obj.adapter));
    }
}
