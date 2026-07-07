using Cove.Engine.Agents;
using Cove.Engine.Hooks;

namespace Cove.Engine.Activity;

public enum AgentStatus
{
    Idle,
    Working,
    WaitingForInput,
    NeedsPermission,
    Stopped,
    Crashed,
}

public sealed record ActivityCard(
    string PaneId,
    string Adapter,
    string? Name,
    string? Workspace,
    string? Room,
    AgentStatus Status,
    string? StopReason,
    int ActiveSubagents,
    string? LastEvent,
    System.DateTimeOffset LastEventAt);

public sealed record ActivityWorkspaceGroup(string Workspace, IReadOnlyList<ActivityCard> Cards);

public sealed class ActivityAggregate
{
    private readonly HookEventRouter _hookRouter;
    private readonly AgentMessageRouter _agentRouter;

    public ActivityAggregate(HookEventRouter hookRouter, AgentMessageRouter agentRouter)
    {
        _hookRouter = hookRouter;
        _agentRouter = agentRouter;
    }

    public IEnumerable<ActivityCard> List()
    {
        var paneStates = _hookRouter.GetAllPaneStates();
        var agents = _agentRouter.List("all").ToDictionary(a => a.PaneId);
        var cards = new List<ActivityCard>();
        foreach (var (paneId, state) in paneStates)
        {
            agents.TryGetValue(paneId, out var agent);
            cards.Add(BuildCard(paneId, state, agent));
        }
        foreach (var agent in agents.Values)
        {
            if (paneStates.ContainsKey(agent.PaneId))
                continue;
            cards.Add(BuildCard(agent.PaneId, null, agent));
        }
        return cards.OrderBy(c => c.Workspace ?? "").ThenBy(c => c.PaneId);
    }

    public IEnumerable<ActivityWorkspaceGroup> Grouped()
    {
        return List()
            .GroupBy(c => c.Workspace ?? "default")
            .OrderBy(g => g.Key)
            .Select(g => new ActivityWorkspaceGroup(g.Key, g.OrderBy(c => c.PaneId).ToList()));
    }

    public bool NeedsInput(string paneId)
    {
        var status = ResolveStatus(paneId);
        return status == AgentStatus.WaitingForInput || status == AgentStatus.NeedsPermission;
    }

    public IEnumerable<ActivityCard> NeedsInputCards()
    {
        return List().Where(c => c.Status == AgentStatus.WaitingForInput || c.Status == AgentStatus.NeedsPermission);
    }

    public AgentStatus ResolveStatus(string paneId)
    {
        var state = _hookRouter.GetPaneState(paneId);
        if (state is null)
            return AgentStatus.Idle;
        return MapStatus(state.Status, state.ActiveSubagents);
    }

    private static AgentStatus MapStatus(string hookStatus, int activeSubagents)
    {
        return hookStatus switch
        {
            "active" => AgentStatus.Working,
            "idle" => AgentStatus.Idle,
            "needs-input" => activeSubagents > 0 ? AgentStatus.Working : AgentStatus.WaitingForInput,
            "error" => AgentStatus.Crashed,
            "tool-running" => AgentStatus.Working,
            _ => AgentStatus.Idle,
        };
    }

    private static ActivityCard BuildCard(string paneId, PaneAgentState? state, AgentInfo? agent)
    {
        var adapter = agent?.Adapter ?? state?.Adapter ?? "unknown";
        var status = state is null ? AgentStatus.Idle : MapStatus(state.Status, state.ActiveSubagents);
        var stopReason = status == AgentStatus.Crashed ? state?.StopReason : null;
        return new ActivityCard(
            paneId,
            adapter,
            agent?.Name,
            agent?.Workspace,
            agent?.Room,
            status,
            stopReason,
            state?.ActiveSubagents ?? 0,
            state?.Status,
            state?.LastEventAt ?? System.DateTimeOffset.UtcNow);
    }
}
