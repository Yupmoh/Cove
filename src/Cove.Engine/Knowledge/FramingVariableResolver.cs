using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed class FramingVariableResolver
{
    private static readonly Regex VariablePattern = new(
        @"\{(\w+)\}",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private readonly ILogger _logger;

    public FramingVariableResolver(ILogger? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    public string Resolve(string template, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        if (variables.Count == 0)
            return template;

        return VariablePattern.Replace(template, match =>
        {
            var name = match.Groups[1].Value;
            if (variables.TryGetValue(name, out var value))
                return value;
            _logger.LogWarning("framing: variable {{{name}}} not found in context", name);
            return match.Value;
        });
    }

    public IReadOnlyList<string> ExtractVariableNames(string template)
    {
        if (string.IsNullOrEmpty(template))
            return [];

        var names = new System.Collections.Generic.HashSet<string>();
        foreach (Match m in VariablePattern.Matches(template))
        {
            names.Add(m.Groups[1].Value);
        }
        return names.ToList();
    }

    public bool HasUnresolvedVariables(string resolved)
    {
        if (string.IsNullOrEmpty(resolved))
            return false;
        return VariablePattern.IsMatch(resolved);
    }
}
