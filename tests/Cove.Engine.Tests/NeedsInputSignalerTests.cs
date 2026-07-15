using Cove.Adapters;
using Cove.Engine.Activity;
using Cove.Engine.Hooks;
using Cove.Engine.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
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
        public int Delivered { get; private set; }
        public string? LastDeliveredId { get; private set; }
        public string? LastDeliveredNookId { get; private set; }
        public void BroadcastNeedsInputSignal(string nookId, string adapter) => SignalsSent++;
        public void BroadcastDockBadge(string nookId, string adapter) => BadgesSent++;
        public void ClearNeedsInputSignal(string nookId) => SignalsCleared++;
        public void ClearDockBadge() => BadgesCleared++;
        public void DeliverNotification(string id, string title, string body, string nookId)
        {
            Delivered++;
            LastDeliveredId = id;
            LastDeliveredNookId = nookId;
        }
    }

    private static NotificationPolicyEngine NewPolicy()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-notif-signaler-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        return new NotificationPolicyEngine(dir, NullLogger.Instance);
    }

    private static ActivityAggregate NewAggregate(HookEventRouter router)
    {
        var agentRouter = new Cove.Engine.Agents.AgentMessageRouter();
        return new ActivityAggregate(router, agentRouter);
    }

    private static HookEvent Ev(string nookId, string adapter, string evt) => new()
    {
        NookId = nookId,
        Adapter = adapter,
        Event = evt,
    };

    [Fact]
    public void Signal_FiresOnNeedsInputTransition_WhenNookNotFocused()
    {
        var router = new HookEventRouter();
        var aggregate = NewAggregate(router);
        var bus = new CapturingBus();
        var signaler = new NeedsInputSignaler(aggregate, bus, () => null);

        router.Route(Ev("nook-1", "claude-code", "session-start"));
        router.Route(Ev("nook-1", "claude-code", "permission-request"));
        signaler.CheckAndSignal("nook-1");

        Assert.Equal(1, bus.SignalsSent);
        Assert.Equal(1, bus.BadgesSent);
    }

    [Fact]
    public void Signal_QuietWhenNookFocused()
    {
        var router = new HookEventRouter();
        var aggregate = NewAggregate(router);
        var bus = new CapturingBus();
        var signaler = new NeedsInputSignaler(aggregate, bus, () => "nook-1");

        router.Route(Ev("nook-1", "claude-code", "session-start"));
        router.Route(Ev("nook-1", "claude-code", "notification"));
        signaler.CheckAndSignal("nook-1");

        Assert.Equal(0, bus.SignalsSent);
    }

    [Fact]
    public void Signal_DoesNotReFire_WhenAlreadySignaled()
    {
        var router = new HookEventRouter();
        var aggregate = NewAggregate(router);
        var bus = new CapturingBus();
        var signaler = new NeedsInputSignaler(aggregate, bus, () => null);

        router.Route(Ev("nook-1", "claude-code", "session-start"));
        router.Route(Ev("nook-1", "claude-code", "notification"));
        signaler.CheckAndSignal("nook-1");
        signaler.CheckAndSignal("nook-1");

        Assert.Equal(1, bus.SignalsSent);
    }

    [Fact]
    public void Signal_ClearsOnTurnEnd_AndCanRefire()
    {
        var router = new HookEventRouter();
        var aggregate = NewAggregate(router);
        var bus = new CapturingBus();
        var signaler = new NeedsInputSignaler(aggregate, bus, () => null);

        router.Route(Ev("nook-1", "claude-code", "session-start"));
        router.Route(Ev("nook-1", "claude-code", "notification"));
        signaler.CheckAndSignal("nook-1");

        router.Route(Ev("nook-1", "claude-code", "user-prompt-submit"));
        signaler.ClearSignal("nook-1");
        Assert.Equal(1, bus.BadgesCleared);

        router.Route(Ev("nook-1", "claude-code", "notification"));
        signaler.CheckAndSignal("nook-1");

        Assert.Equal(2, bus.SignalsSent);
    }

    [Fact]
    public void Deliver_FiresOsNotification_WhenPolicyAllows_CorrelatingWithNookId()
    {
        var router = new HookEventRouter();
        var aggregate = NewAggregate(router);
        var bus = new CapturingBus();
        var policy = NewPolicy();
        var signaler = new NeedsInputSignaler(aggregate, bus, () => null, policy);

        router.Route(Ev("nook-1", "claude-code", "session-start"));
        router.Route(Ev("nook-1", "claude-code", "notification"));
        signaler.CheckAndSignal("nook-1");

        Assert.Equal(1, bus.Delivered);
        Assert.Equal("nook-1", bus.LastDeliveredId);
        Assert.Equal("nook-1", bus.LastDeliveredNookId);
    }

    [Fact]
    public void Deliver_Suppressed_WhenOsNotificationTierDisabled()
    {
        var router = new HookEventRouter();
        var aggregate = NewAggregate(router);
        var bus = new CapturingBus();
        var policy = NewPolicy();
        policy.SetTierEnabled(NotificationTier.OsNotification, false);
        var signaler = new NeedsInputSignaler(aggregate, bus, () => null, policy);

        router.Route(Ev("nook-1", "claude-code", "session-start"));
        router.Route(Ev("nook-1", "claude-code", "notification"));
        signaler.CheckAndSignal("nook-1");

        Assert.Equal(1, bus.BadgesSent);
        Assert.Equal(0, bus.Delivered);
    }

    [Fact]
    public void Deliver_DoesNotFire_WhenNoPolicyProvided()
    {
        var router = new HookEventRouter();
        var aggregate = NewAggregate(router);
        var bus = new CapturingBus();
        var signaler = new NeedsInputSignaler(aggregate, bus, () => null);

        router.Route(Ev("nook-1", "claude-code", "session-start"));
        router.Route(Ev("nook-1", "claude-code", "notification"));
        signaler.CheckAndSignal("nook-1");

        Assert.Equal(0, bus.Delivered);
    }
}
