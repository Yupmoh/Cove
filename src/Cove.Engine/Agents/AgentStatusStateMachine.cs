using Cove.Engine.Activity;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Agents;

public sealed record SubagentState(string Id, bool IsIdle, AgentStatus Status);

public sealed class AgentStatusStateMachine
{
    private readonly ILogger _logger;

    public AgentStatusStateMachine(ILogger? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    public AgentStatus ComputeAggregateStatus(AgentStatus adapterStatus, IReadOnlyList<SubagentState> subagents)
    {
        if (adapterStatus == AgentStatus.Crashed)
            return AgentStatus.Crashed;
        if (adapterStatus == AgentStatus.Stopped)
            return AgentStatus.Stopped;
        if (adapterStatus == AgentStatus.NeedsPermission)
            return AgentStatus.NeedsPermission;

        if (subagents.Count == 0)
        {
            return adapterStatus;
        }

        var allSubagentsIdle = true;
        var anySubagentNeedsInput = false;
        var anySubagentNeedsPermission = false;
        var anySubagentCrashed = false;

        foreach (var sub in subagents)
        {
            if (!sub.IsIdle)
                allSubagentsIdle = false;
            if (sub.Status == AgentStatus.WaitingForInput)
                anySubagentNeedsInput = true;
            if (sub.Status == AgentStatus.NeedsPermission)
                anySubagentNeedsPermission = true;
            if (sub.Status == AgentStatus.Crashed)
                anySubagentCrashed = true;
        }

        if (anySubagentCrashed)
            return AgentStatus.Crashed;
        if (anySubagentNeedsPermission)
            return AgentStatus.NeedsPermission;
        if (anySubagentNeedsInput)
            return AgentStatus.WaitingForInput;

        if (adapterStatus == AgentStatus.Working && !allSubagentsIdle)
            return AgentStatus.Working;

        if (adapterStatus == AgentStatus.Working && allSubagentsIdle)
        {
            _logger.LogInformation("agent-status: adapter working but all {count} subagents idle → waiting", subagents.Count);
            return AgentStatus.WaitingForInput;
        }

        if (adapterStatus == AgentStatus.WaitingForInput)
            return AgentStatus.WaitingForInput;

        if (adapterStatus == AgentStatus.Idle)
            return AgentStatus.Idle;

        return adapterStatus;
    }

    public AgentStatus TransitionTo(AgentStatus current, AgentStatus target)
    {
        if (current == AgentStatus.Crashed || current == AgentStatus.Stopped)
        {
            _logger.LogWarning("agent-status: cannot transition from terminal state {current} to {target}", current, target);
            return current;
        }

        if (current == target)
            return current;

        _logger.LogInformation("agent-status: {current} → {target}", current, target);
        return target;
    }

    public bool IsTerminal(AgentStatus status)
    {
        return status == AgentStatus.Crashed || status == AgentStatus.Stopped;
    }

    public bool CanTransition(AgentStatus from, AgentStatus to)
    {
        if (IsTerminal(from))
            return false;
        return true;
    }
}
