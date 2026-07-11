using System.Text.RegularExpressions;
using Cove.Protocol;

namespace Cove.Engine.Knowledge;

public sealed class TimelineValidationException(string code, string message) : System.Exception(message)
{
    public string Code { get; } = code;
}

public sealed partial class TimelineValidator
{
    [GeneratedRegex(@"^[a-z][a-z0-9-]*:[a-zA-Z0-9_-]+$", RegexOptions.Compiled)]
    private static partial Regex TagPattern();

    private static readonly System.Collections.Generic.HashSet<string> ValidScopes = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "bay", "shore", "nook", "task", "session",
    };

    public void Validate(TimelineEntry entry)
    {
        if (!string.IsNullOrEmpty(entry.Scope))
        {
            var scopeBase = entry.Scope.Contains(':') ? entry.Scope.AsSpan(0, entry.Scope.IndexOf(':')).ToString() : entry.Scope;
            if (!ValidScopes.Contains(scopeBase))
                throw new TimelineValidationException("invalid_scope", $"scope '{entry.Scope}' is not a valid canonical scope (bay/shore/nook/task/session)");
        }

        if (entry.Tags is { } tags)
        {
            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                    throw new TimelineValidationException("invalid_tag", "tag cannot be empty");
                if (!TagPattern().IsMatch(tag))
                    throw new TimelineValidationException("invalid_tag", $"tag '{tag}' does not match required pattern ^[a-z][a-z0-9-]*:[a-zA-Z0-9_-]+$");
            }
        }
    }
}
