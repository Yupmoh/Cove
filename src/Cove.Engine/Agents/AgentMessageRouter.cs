namespace Cove.Engine.Agents;

public sealed record AgentMessageSender(string PaneId, string Adapter, string? Name);

public sealed record AgentInfo(
    string PaneId,
    string Adapter,
    string? Name,
    string? Workspace,
    string? Room,
    string Status,
    bool McpVisible,
    string McpAccessScope);

public sealed class AgentMessageFramer
{
    public static string Frame(AgentMessageSender sender, string body, string? replyPrefix)
    {
        var identity = !string.IsNullOrEmpty(sender.Name) ? sender.Name : sender.PaneId;
        if (replyPrefix is null)
            return $"[Message from {identity} ({sender.Adapter}) via cove]\n{body}";
        return $"[Message from {identity} ({sender.Adapter}) via cove]\n{body}\n[Reply with: cove agent message {replyPrefix} \"<your reply>\"]";
    }

    public static string NoFrame(string body) => body;
}

public sealed class AgentMessageRouter
{
    private readonly Dictionary<string, AgentInfo> _agents = new();

    public void Register(string paneId, string adapter, string? name, string? workspace = null, string? room = null, string status = "idle", bool mcpVisible = true, string mcpAccessScope = "same-tab")
    {
        _agents[paneId] = new AgentInfo(paneId, adapter, name, workspace, room, status, mcpVisible, mcpAccessScope);
    }

    public void Unregister(string paneId)
    {
        _agents.Remove(paneId);
    }

    public void UpdateStatus(string paneId, string status)
    {
        if (_agents.TryGetValue(paneId, out var existing))
            _agents[paneId] = existing with { Status = status };
    }

    public AgentInfo? ResolveTarget(string paneIdOrPrefix)
    {
        if (_agents.TryGetValue(paneIdOrPrefix, out var exact))
            return exact;
        var prefixMatches = _agents.Keys.Where(k => k.StartsWith(paneIdOrPrefix, System.StringComparison.Ordinal)).ToList();
        if (prefixMatches.Count == 1)
            return _agents[prefixMatches[0]];
        return null;
    }

    public System.Collections.Generic.IEnumerable<AgentInfo> List(string scope = "all", string? requesterWorkspace = null, string? requesterPaneId = null, string? requesterRoom = null)
    {
        var query = _agents.Values.Where(a => a.McpVisible);

        if (scope == "same-tab" && requesterRoom is not null)
            query = query.Where(a => a.Room == requesterRoom && a.PaneId != requesterPaneId);
        else if (scope == "same-workspace" && requesterWorkspace is not null)
            query = query.Where(a => a.Workspace == requesterWorkspace);

        return query;
    }
}
