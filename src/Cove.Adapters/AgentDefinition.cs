using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
namespace Cove.Adapters;

public sealed record AgentDefinition(
    string Slug,
    string Name,
    string Description,
    string Adapter,
    string Prompt,
    IReadOnlyList<string> AttachedSkills);

public sealed record AgentValidationError(string Field, string Code, string Message);

public sealed partial class AgentDefinitionValidator
{
    [GeneratedRegex(@"^[a-z0-9-]{1,64}$", RegexOptions.None)]
    private static partial Regex SlugRegex();


    public static List<AgentValidationError> Validate(AgentDefinition agent)
    {
        var errors = new List<AgentValidationError>();

        if (string.IsNullOrEmpty(agent.Slug) || !SlugRegex().IsMatch(agent.Slug))
            errors.Add(new AgentValidationError("slug", "invalid_slug", "slug must be kebab-case, 1-64 chars [a-z0-9-]"));

        if (string.IsNullOrEmpty(agent.Name))
            errors.Add(new AgentValidationError("name", "missing_name", "name is required"));

        if (string.IsNullOrEmpty(agent.Adapter))
            errors.Add(new AgentValidationError("adapter", "missing_adapter", "adapter is required"));

        if (string.IsNullOrEmpty(agent.Prompt))
            errors.Add(new AgentValidationError("prompt", "missing_prompt", "prompt is required"));

        return errors;
    }
    public static bool IsValidSlug(string slug) => SlugRegex().IsMatch(slug);
}

public static class AgentDefinitionParser
{
    public static AgentDefinition? Parse(string content)
    {
        if (!content.StartsWith("---", StringComparison.Ordinal))
            return null;

        var end = content.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0)
            return null;

        var frontmatter = content.AsSpan(3, end - 3).ToString();
        var body = content.AsSpan(end + 4).Trim().ToString();

        string? slug = null, name = null, description = null, adapter = null;
        var skills = new List<string>();

        foreach (var line in frontmatter.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.TrimEnd('\r');
            var colon = trimmed.IndexOf(':');
            if (colon < 0) continue;
            var key = trimmed.AsSpan(0, colon).Trim().ToString();
            var value = trimmed.AsSpan(colon + 1).Trim().Trim('"').Trim('\'').ToString();

            switch (key)
            {
                case "slug": slug = value; break;
                case "name": name = value; break;
                case "description": description = value; break;
                case "adapter": adapter = value; break;
                case "attachedSkills":
                    var inner = value.Trim('[', ']', ' ');
                    if (!string.IsNullOrEmpty(inner))
                        skills.AddRange(inner.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
            }
        }

        if (string.IsNullOrEmpty(slug) || string.IsNullOrEmpty(adapter))
            return null;

        return new AgentDefinition(slug, name ?? slug, description ?? "", adapter, body, skills);
    }

    public static string Serialize(AgentDefinition agent)
    {
        var skills = agent.AttachedSkills.Count > 0
            ? $"attachedSkills: [{string.Join(", ", agent.AttachedSkills)}]\n"
            : "";
        return $$"""
        ---
        slug: {{agent.Slug}}
        name: {{agent.Name}}
        description: {{agent.Description}}
        adapter: {{agent.Adapter}}
        {{skills}}---

        {{agent.Prompt}}
        """;
    }
}
public sealed class AgentDefinitionStore
{
    private readonly string _root;
    private readonly ILogger? _logger;

    public AgentDefinitionStore(string root, ILogger? logger = null)
    {
        _root = root;
        _logger = logger;
    }

    public AgentDefinition? Load(string slug)
    {
        if (!AgentDefinitionValidator.IsValidSlug(slug))
        {
            _logger?.AgentLoadInvalidSlug(slug);
            return null;
        }
        var path = Path.Combine(_root, slug, "agent.md");
        if (!File.Exists(path))
            return null;
        var content = File.ReadAllText(path);
        return AgentDefinitionParser.Parse(content);
    }

    public List<AgentDefinition> List()
    {
        var agents = new List<AgentDefinition>();
        if (!Directory.Exists(_root))
            return agents;

        foreach (var dir in Directory.EnumerateDirectories(_root))
        {
            var path = Path.Combine(dir, "agent.md");
            if (File.Exists(path))
            {
                var agent = AgentDefinitionParser.Parse(File.ReadAllText(path));
                if (agent is not null)
                    agents.Add(agent);
            }
        }
        return agents;
    }

    public void Save(AgentDefinition agent)
    {
        if (!AgentDefinitionValidator.IsValidSlug(agent.Slug))
            throw new ArgumentException($"invalid slug: {agent.Slug}");
        var dir = Path.Combine(_root, agent.Slug);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "agent.md"), AgentDefinitionParser.Serialize(agent));
    }

    public void Delete(string slug)
    {
        if (!AgentDefinitionValidator.IsValidSlug(slug))
        {
            _logger?.AgentDeleteInvalidSlug(slug);
            return;
        }
        var dir = Path.Combine(_root, slug);
        if (Directory.Exists(dir))
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (IOException ex) { _logger?.AgentDeleteFailed(slug, ex.Message); }
        }
    }

    public string GetPath(string slug) => Path.Combine(_root, slug, "agent.md");
}
