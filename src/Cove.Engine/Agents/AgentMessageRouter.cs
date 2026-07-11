namespace Cove.Engine.Agents;

public sealed record AgentMessageSender(string NookId, string Adapter, string? Name);

public sealed record AgentInfo(
    string NookId,
    string Adapter,
    string? Name,
    string? Bay,
    string? Shore,
    string Status,
    bool McpVisible,
    string McpAccessScope);

public sealed class AgentMessageFramer
{
    public static string Frame(AgentMessageSender sender, string body, string? replyPrefix)
    {
        var identity = !string.IsNullOrEmpty(sender.Name) ? sender.Name : sender.NookId;
        if (replyPrefix is null)
            return $"[Message from {identity} ({sender.Adapter}) via cove]\n{body}";
        return $"[Message from {identity} ({sender.Adapter}) via cove]\n{body}\n[Reply with: cove agent message {replyPrefix} \"<your reply>\"]";
    }

    public static string NoFrame(string body) => body;
}

public sealed class AgentMessageRouter
{
    private readonly Dictionary<string, AgentInfo> _agents = new();

    public void Register(string nookId, string adapter, string? name, string? bay = null, string? shore = null, string status = "idle", bool mcpVisible = true, string mcpAccessScope = "same-tab")
    {
        _agents[nookId] = new AgentInfo(nookId, adapter, name, bay, shore, status, mcpVisible, mcpAccessScope);
    }

    public void Unregister(string nookId)
    {
        _agents.Remove(nookId);
    }

    public void UpdateStatus(string nookId, string status)
    {
        if (_agents.TryGetValue(nookId, out var existing))
            _agents[nookId] = existing with { Status = status };
    }

    public AgentInfo? ResolveTarget(string nookIdOrPrefix)
    {
        if (_agents.TryGetValue(nookIdOrPrefix, out var exact))
            return exact;
        var prefixMatches = _agents.Keys.Where(k => k.StartsWith(nookIdOrPrefix, System.StringComparison.Ordinal)).ToList();
        if (prefixMatches.Count == 1)
            return _agents[prefixMatches[0]];
        return null;
    }

    public System.Collections.Generic.IEnumerable<AgentInfo> List(string scope = "all", string? requesterBay = null, string? requesterNookId = null, string? requesterShore = null)
    {
        var query = _agents.Values.Where(a => a.McpVisible);

        if (scope == "same-tab" && requesterShore is not null)
            query = query.Where(a => a.Shore == requesterShore && a.NookId != requesterNookId);
        else if (scope == "same-bay" && requesterBay is not null)
            query = query.Where(a => a.Bay == requesterBay);

        return query;
    }
}
