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
    string NookId,
    string Adapter,
    string? Name,
    string? Bay,
    string? Shore,
    AgentStatus Status,
    string? StopReason,
    int ActiveSubagents,
    string? LastEvent,
    System.DateTimeOffset LastEventAt);

public sealed record ActivityBayGroup(string Bay, IReadOnlyList<ActivityCard> Cards);

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
        var nookStates = _hookRouter.GetAllNookStates();
        var agents = _agentRouter.List("all").ToDictionary(a => a.NookId);
        var cards = new List<ActivityCard>();
        foreach (var (nookId, state) in nookStates)
        {
            agents.TryGetValue(nookId, out var agent);
            cards.Add(BuildCard(nookId, state, agent));
        }
        foreach (var agent in agents.Values)
        {
            if (nookStates.ContainsKey(agent.NookId))
                continue;
            cards.Add(BuildCard(agent.NookId, null, agent));
        }
        return cards.OrderBy(c => c.Bay ?? "").ThenBy(c => c.NookId);
    }

    public IEnumerable<ActivityBayGroup> Grouped()
    {
        return List()
            .GroupBy(c => c.Bay ?? "default")
            .OrderBy(g => g.Key)
            .Select(g => new ActivityBayGroup(g.Key, g.OrderBy(c => c.NookId).ToList()));
    }

    public bool NeedsInput(string nookId)
    {
        var status = ResolveStatus(nookId);
        return status == AgentStatus.WaitingForInput || status == AgentStatus.NeedsPermission;
    }

    public IEnumerable<ActivityCard> NeedsInputCards()
    {
        return List().Where(c => c.Status == AgentStatus.WaitingForInput || c.Status == AgentStatus.NeedsPermission);
    }

    public AgentStatus ResolveStatus(string nookId)
    {
        var state = _hookRouter.GetNookState(nookId);
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
            "needs-permission" => AgentStatus.NeedsPermission,
            "done" => AgentStatus.Stopped,
            "error" => AgentStatus.Crashed,
            "tool-running" => AgentStatus.Working,
            _ => AgentStatus.Idle,
        };
    }

    private static ActivityCard BuildCard(string nookId, NookAgentState? state, AgentInfo? agent)
    {
        var adapter = agent?.Adapter ?? state?.Adapter ?? "unknown";
        var status = state is null ? AgentStatus.Idle : MapStatus(state.Status, state.ActiveSubagents);
        var stopReason = status == AgentStatus.Crashed ? state?.StopReason : null;
        return new ActivityCard(
            nookId,
            adapter,
            agent?.Name,
            agent?.Bay,
            agent?.Shore,
            status,
            stopReason,
            state?.ActiveSubagents ?? 0,
            state?.Status,
            state?.LastEventAt ?? System.DateTimeOffset.UtcNow);
    }
}
