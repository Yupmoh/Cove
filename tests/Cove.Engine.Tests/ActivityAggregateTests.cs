using System.Text.Json;
using Cove.Adapters;
using Cove.Engine.Activity;
using Cove.Engine.Agents;
using Cove.Engine.Hooks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace Cove.Engine.Tests;

public sealed class ActivityAggregateTests
{
    private static ActivityAggregate NewAggregate()
    {
        var hookRouter = new HookEventRouter(NullLogger.Instance);
        var agentRouter = new AgentMessageRouter();
        return new ActivityAggregate(hookRouter, agentRouter);
    }

    private static ActivityAggregate WithAgent(out HookEventRouter hookRouter, out AgentMessageRouter agentRouter, string nookId, string adapter, string bay = "ws1", string shore = "shore1")
    {
        hookRouter = new HookEventRouter(NullLogger.Instance);
        agentRouter = new AgentMessageRouter();
        agentRouter.Register(nookId, adapter, "A", bay, shore);
        return new ActivityAggregate(hookRouter, agentRouter);
    }

    [Fact]
    public void List_AgentWithoutHookState_DefaultsToIdle()
    {
        var agg = WithAgent(out _, out _, "p1", "claude-code");
        var card = agg.List().First();
        Assert.Equal(AgentStatus.Idle, card.Status);
        Assert.Equal("claude-code", card.Adapter);
    }

    [Fact]
    public void ResolveStatus_ActiveHookState_MapsToWorking()
    {
        var agg = WithAgent(out var hooks, out _, "p1", "claude-code");
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "pre-tool-use", NookId = "p1" });
        Assert.Equal(AgentStatus.Working, agg.ResolveStatus("p1"));
    }

    [Fact]
    public void ResolveStatus_NeedsInput_MapsToWaitingForInput()
    {
        var agg = WithAgent(out var hooks, out _, "p1", "claude-code");
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "notification", NookId = "p1" });
        Assert.Equal(AgentStatus.WaitingForInput, agg.ResolveStatus("p1"));
    }

    [Fact]
    public void ResolveStatus_PermissionRequest_MapsToNeedsPermission()
    {
        var agg = WithAgent(out var hooks, out _, "p1", "claude-code");
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "permission-request", NookId = "p1" });
        Assert.Equal(AgentStatus.NeedsPermission, agg.ResolveStatus("p1"));
    }

    [Fact]
    public void ResolveStatus_Stop_MapsToStopped()
    {
        var agg = WithAgent(out var hooks, out _, "p1", "claude-code");
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "stop", NookId = "p1" });
        Assert.Equal(AgentStatus.Stopped, agg.ResolveStatus("p1"));
    }

    [Fact]
    public void ResolveStatus_NeedsInputWithActiveSubagent_StaysWorking()
    {
        var agg = WithAgent(out var hooks, out _, "p1", "claude-code");
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "subagent-start", NookId = "p1" });
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "notification", NookId = "p1" });
        Assert.Equal(AgentStatus.Working, agg.ResolveStatus("p1"));
    }

    [Fact]
    public void ResolveStatus_Error_MapsToCrashed()
    {
        var agg = WithAgent(out var hooks, out _, "p1", "claude-code");
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "stop-failure", NookId = "p1" });
        Assert.Equal(AgentStatus.Crashed, agg.ResolveStatus("p1"));
    }

    [Fact]
    public void ResolveStatus_IdleHookState_MapsToIdle()
    {
        var agg = WithAgent(out var hooks, out _, "p1", "claude-code");
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "session-end", NookId = "p1" });
        Assert.Equal(AgentStatus.Idle, agg.ResolveStatus("p1"));
    }

    [Fact]
    public void NeedsInput_WaitingForInput_ReturnsTrue()
    {
        var agg = WithAgent(out var hooks, out _, "p1", "claude-code");
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "notification", NookId = "p1" });
        Assert.True(agg.NeedsInput("p1"));
    }

    [Fact]
    public void NeedsInput_Working_ReturnsFalse()
    {
        var agg = WithAgent(out var hooks, out _, "p1", "claude-code");
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "pre-tool-use", NookId = "p1" });
        Assert.False(agg.NeedsInput("p1"));
    }

    [Fact]
    public void NeedsInputCards_ReturnsAllBlockedAgents()
    {
        var hookRouter = new HookEventRouter(NullLogger.Instance);
        var agentRouter = new AgentMessageRouter();
        agentRouter.Register("p1", "claude-code", "A", "ws1", "shore1");
        agentRouter.Register("p2", "codex", "B", "ws1", "shore1");
        agentRouter.Register("p3", "gemini", "C", "ws1", "shore1");
        hookRouter.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        hookRouter.Route(new HookEvent { Adapter = "claude-code", Event = "notification", NookId = "p1" });
        hookRouter.Route(new HookEvent { Adapter = "codex", Event = "session-start", NookId = "p2" });
        hookRouter.Route(new HookEvent { Adapter = "codex", Event = "permission-request", NookId = "p2" });
        hookRouter.Route(new HookEvent { Adapter = "gemini", Event = "session-start", NookId = "p3" });
        hookRouter.Route(new HookEvent { Adapter = "gemini", Event = "pre-tool-use", NookId = "p3" });

        var agg = new ActivityAggregate(hookRouter, agentRouter);
        var needsInput = agg.NeedsInputCards().ToList();
        Assert.Equal(2, needsInput.Count);
    }

    [Fact]
    public void List_GroupedByBay()
    {
        var hookRouter = new HookEventRouter(NullLogger.Instance);
        var agentRouter = new AgentMessageRouter();
        agentRouter.Register("p1", "claude-code", "A", "ws1", "shore1");
        agentRouter.Register("p2", "codex", "B", "ws2", "shore1");
        var agg = new ActivityAggregate(hookRouter, agentRouter);
        var groups = agg.Grouped().ToList();
        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, g => g.Bay == "ws1" && g.Cards.Count == 1);
        Assert.Contains(groups, g => g.Bay == "ws2" && g.Cards.Count == 1);
    }

    [Fact]
    public void ResolveStatus_UnknownNook_ReturnsIdle()
    {
        var agg = NewAggregate();
        Assert.Equal(AgentStatus.Idle, agg.ResolveStatus("nonexistent"));
    }

    [Fact]
    public void ResolveStatus_StopFailureWithReason_MapsToCrashedWithReason()
    {
        var agg = WithAgent(out var hooks, out _, "p1", "claude-code");
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "stop-failure", NookId = "p1", Payload = JsonDocument.Parse("""{"reason":"rate-limited"}""").RootElement });
        var card = agg.List().First();
        Assert.Equal(AgentStatus.Crashed, card.Status);
        Assert.Equal("rate-limited", card.StopReason);
    }

    [Fact]
    public void ResolveStatus_StopFailureWithoutReason_MapsToCrashedNoReason()
    {
        var agg = WithAgent(out var hooks, out _, "p1", "claude-code");
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", NookId = "p1" });
        hooks.Route(new HookEvent { Adapter = "claude-code", Event = "stop-failure", NookId = "p1" });
        var card = agg.List().First();
        Assert.Equal(AgentStatus.Crashed, card.Status);
        Assert.Null(card.StopReason);
    }
}
