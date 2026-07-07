namespace Cove.Engine.Protocol;

public sealed record PrefixResolveResult(bool Found, string? Id, string? ErrorCode);

public sealed class PrefixResolver
{
    private readonly Dictionary<string, List<string>> _byType = new();

    public void Index(string type, string id)
    {
        if (!_byType.TryGetValue(type, out var list))
        {
            list = new List<string>();
            _byType[type] = list;
        }
        if (!list.Contains(id))
            list.Add(id);
    }

    public void Clear(string type)
    {
        _byType.Remove(type);
    }

    public PrefixResolveResult Resolve(string type, string prefix, bool rejectPrefix = false)
    {
        if (string.IsNullOrEmpty(prefix))
            return new PrefixResolveResult(false, null, "not_found");

        if (rejectPrefix)
            return new PrefixResolveResult(false, null, "not_found");

        if (!_byType.TryGetValue(type, out var list))
            return new PrefixResolveResult(false, null, "not_found");

        var matches = list.Where(id => id.StartsWith(prefix, System.StringComparison.Ordinal)).ToList();
        if (matches.Count == 0)
            return new PrefixResolveResult(false, null, "not_found");
        if (matches.Count > 1)
            return new PrefixResolveResult(false, null, "ambiguous_id");
        return new PrefixResolveResult(true, matches[0], null);
    }
}
