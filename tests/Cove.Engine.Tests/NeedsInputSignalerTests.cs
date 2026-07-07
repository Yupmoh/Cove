using Cove.Adapters;
using Cove.Engine.Activity;
using Cove.Engine.Hooks;
using Xunit;
namespace Cove.Engine.Tests;

public sealed class NeedsInputSignalerTests
{
    private sealed class CapturingBus : INotificationBus
    {
        public int SignalsSent { get; private set; }
        public int BadgesSent { get; private set; }
        public int BadgesCleared { get; private set; }
        public int SignalsCleared { get; private set; }
        public void BroadcastNeedsInputSignal(string paneId, string adapter) => SignalsSent++;
        public void BroadcastDockBadge(string paneId, string adapter) => BadgesSent++;
        public void ClearNeedsInputSignal(string paneId) => SignalsCleared++;
        public void ClearDockBadge() => BadgesCleared++;
    }

    private static ActivityAggregate NewAggregate(HookEventRouter router)
    {
        var agentRouter = new Cove.Engine.Agents.AgentMessageRouter();
        return new ActivityAggregate(router, agentRouter);
    }

    private static HookEvent Ev(string paneId, string adapter, string evt) => new()
    {
        PaneId = paneId,
        Adapter = adapter,
        Event = evt,
    };

    [Fact]
    public void Signal_FiresOnNeedsInputTransition_WhenPaneNotFocused()
    {
        var router = new HookEventRouter();
        var aggregate = NewAggregate(router);
        var bus = new CapturingBus();
        var signaler = new NeedsInputSignaler(aggregate, bus, () => null);

        router.Route(Ev("pane-1", "claude-code", "session-start"));
        router.Route(Ev("pane-1", "claude-code", "stop"));
        signaler.CheckAndSignal("pane-1");

        Assert.Equal(1, bus.SignalsSent);
        Assert.Equal(1, bus.BadgesSent);
    }

    [Fact]
    public void Signal_QuietWhenPaneFocused()
    {
        var router = new HookEventRouter();
        var aggregate = NewAggregate(router);
        var bus = new CapturingBus();
        var signaler = new NeedsInputSignaler(aggregate, bus, () => "pane-1");

        router.Route(Ev("pane-1", "claude-code", "session-start"));
        router.Route(Ev("pane-1", "claude-code", "stop"));
        signaler.CheckAndSignal("pane-1");

        Assert.Equal(0, bus.SignalsSent);
    }

    [Fact]
    public void Signal_DoesNotReFire_WhenAlreadySignaled()
    {
        var router = new HookEventRouter();
        var aggregate = NewAggregate(router);
        var bus = new CapturingBus();
        var signaler = new NeedsInputSignaler(aggregate, bus, () => null);

        router.Route(Ev("pane-1", "claude-code", "session-start"));
        router.Route(Ev("pane-1", "claude-code", "stop"));
        signaler.CheckAndSignal("pane-1");
        signaler.CheckAndSignal("pane-1");

        Assert.Equal(1, bus.SignalsSent);
    }

    [Fact]
    public void Signal_ClearsOnTurnEnd_AndCanRefire()
    {
        var router = new HookEventRouter();
        var aggregate = NewAggregate(router);
        var bus = new CapturingBus();
        var signaler = new NeedsInputSignaler(aggregate, bus, () => null);

        router.Route(Ev("pane-1", "claude-code", "session-start"));
        router.Route(Ev("pane-1", "claude-code", "stop"));
        signaler.CheckAndSignal("pane-1");

        router.Route(Ev("pane-1", "claude-code", "user-prompt-submit"));
        signaler.ClearSignal("pane-1");
        Assert.Equal(1, bus.BadgesCleared);

        router.Route(Ev("pane-1", "claude-code", "stop"));
        signaler.CheckAndSignal("pane-1");

        Assert.Equal(2, bus.SignalsSent);
    }
}
