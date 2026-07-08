using Cove.Engine.Activity;
using Cove.Engine.Agents;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class AgentStatusStateMachineTests
{
    [Fact]
    public void Compute_NoSubagents_ReturnsAdapterStatus()
    {
        var sm = new AgentStatusStateMachine(NullLogger.Instance);
        Assert.Equal(AgentStatus.Working, sm.ComputeAggregateStatus(AgentStatus.Working, []));
        Assert.Equal(AgentStatus.Idle, sm.ComputeAggregateStatus(AgentStatus.Idle, []));
    }

    [Fact]
    public void Compute_AdapterCrashed_ReturnsCrashed()
    {
        var sm = new AgentStatusStateMachine(NullLogger.Instance);
        var subs = new List<SubagentState> { new("s1", true, AgentStatus.Idle) };
        Assert.Equal(AgentStatus.Crashed, sm.ComputeAggregateStatus(AgentStatus.Crashed, subs));
    }

    [Fact]
    public void Compute_AdapterStopped_ReturnsStopped()
    {
        var sm = new AgentStatusStateMachine(NullLogger.Instance);
        var subs = new List<SubagentState> { new("s1", true, AgentStatus.Idle) };
        Assert.Equal(AgentStatus.Stopped, sm.ComputeAggregateStatus(AgentStatus.Stopped, subs));
    }

    [Fact]
    public void Compute_AllSubagentsIdle_AdapterWorking_ReturnsWaitingForInput()
    {
        var sm = new AgentStatusStateMachine(NullLogger.Instance);
        var subs = new List<SubagentState>
        {
            new("s1", true, AgentStatus.Idle),
            new("s2", true, AgentStatus.Idle),
        };
        Assert.Equal(AgentStatus.WaitingForInput, sm.ComputeAggregateStatus(AgentStatus.Working, subs));
    }

    [Fact]
    public void Compute_AnySubagentNotIdle_AdapterWorking_ReturnsWorking()
    {
        var sm = new AgentStatusStateMachine(NullLogger.Instance);
        var subs = new List<SubagentState>
        {
            new("s1", true, AgentStatus.Idle),
            new("s2", false, AgentStatus.Working),
        };
        Assert.Equal(AgentStatus.Working, sm.ComputeAggregateStatus(AgentStatus.Working, subs));
    }

    [Fact]
    public void Compute_SubagentNeedsInput_ReturnsWaitingForInput()
    {
        var sm = new AgentStatusStateMachine(NullLogger.Instance);
        var subs = new List<SubagentState>
        {
            new("s1", false, AgentStatus.Working),
            new("s2", false, AgentStatus.WaitingForInput),
        };
        Assert.Equal(AgentStatus.WaitingForInput, sm.ComputeAggregateStatus(AgentStatus.Working, subs));
    }

    [Fact]
    public void Compute_SubagentNeedsPermission_ReturnsNeedsPermission()
    {
        var sm = new AgentStatusStateMachine(NullLogger.Instance);
        var subs = new List<SubagentState>
        {
            new("s1", false, AgentStatus.Working),
            new("s2", false, AgentStatus.NeedsPermission),
        };
        Assert.Equal(AgentStatus.NeedsPermission, sm.ComputeAggregateStatus(AgentStatus.Working, subs));
    }

    [Fact]
    public void Compute_SubagentCrashed_ReturnsCrashed()
    {
        var sm = new AgentStatusStateMachine(NullLogger.Instance);
        var subs = new List<SubagentState>
        {
            new("s1", false, AgentStatus.Working),
            new("s2", false, AgentStatus.Crashed),
        };
        Assert.Equal(AgentStatus.Crashed, sm.ComputeAggregateStatus(AgentStatus.Working, subs));
    }

    [Fact]
    public void Compute_AdapterIdle_AllSubagentsIdle_ReturnsIdle()
    {
        var sm = new AgentStatusStateMachine(NullLogger.Instance);
        var subs = new List<SubagentState>
        {
            new("s1", true, AgentStatus.Idle),
            new("s2", true, AgentStatus.Idle),
        };
        Assert.Equal(AgentStatus.Idle, sm.ComputeAggregateStatus(AgentStatus.Idle, subs));
    }

    [Fact]
    public void TransitionTo_SameStatus_ReturnsSame()
    {
        var sm = new AgentStatusStateMachine(NullLogger.Instance);
        Assert.Equal(AgentStatus.Working, sm.TransitionTo(AgentStatus.Working, AgentStatus.Working));
    }

    [Fact]
    public void TransitionTo_FromTerminalState_ReturnsCurrent()
    {
        var sm = new AgentStatusStateMachine(NullLogger.Instance);
        Assert.Equal(AgentStatus.Crashed, sm.TransitionTo(AgentStatus.Crashed, AgentStatus.Working));
        Assert.Equal(AgentStatus.Stopped, sm.TransitionTo(AgentStatus.Stopped, AgentStatus.Idle));
    }

    [Fact]
    public void TransitionTo_NormalTransition_ReturnsTarget()
    {
        var sm = new AgentStatusStateMachine(NullLogger.Instance);
        Assert.Equal(AgentStatus.WaitingForInput, sm.TransitionTo(AgentStatus.Working, AgentStatus.WaitingForInput));
    }

    [Fact]
    public void IsTerminal_CrashedStopped_ReturnsTrue()
    {
        var sm = new AgentStatusStateMachine(NullLogger.Instance);
        Assert.True(sm.IsTerminal(AgentStatus.Crashed));
        Assert.True(sm.IsTerminal(AgentStatus.Stopped));
        Assert.False(sm.IsTerminal(AgentStatus.Working));
        Assert.False(sm.IsTerminal(AgentStatus.Idle));
    }

    [Fact]
    public void CanTransition_FromTerminal_ReturnsFalse()
    {
        var sm = new AgentStatusStateMachine(NullLogger.Instance);
        Assert.False(sm.CanTransition(AgentStatus.Crashed, AgentStatus.Working));
        Assert.False(sm.CanTransition(AgentStatus.Stopped, AgentStatus.Idle));
    }

    [Fact]
    public void CanTransition_FromNonTerminal_ReturnsTrue()
    {
        var sm = new AgentStatusStateMachine(NullLogger.Instance);
        Assert.True(sm.CanTransition(AgentStatus.Working, AgentStatus.Idle));
        Assert.True(sm.CanTransition(AgentStatus.Idle, AgentStatus.Working));
    }

    [Fact]
    public void Compute_CrashedOverridesNeedsPermission()
    {
        var sm = new AgentStatusStateMachine(NullLogger.Instance);
        var subs = new List<SubagentState>
        {
            new("s1", false, AgentStatus.NeedsPermission),
            new("s2", false, AgentStatus.Crashed),
        };
        Assert.Equal(AgentStatus.Crashed, sm.ComputeAggregateStatus(AgentStatus.Working, subs));
    }
}
