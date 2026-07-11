using Cove.Engine.Activity;
using Cove.Engine.Notifications;

namespace Cove.Engine.Hooks;

public interface INotificationBus
{
    void BroadcastNeedsInputSignal(string nookId, string adapter);
    void BroadcastDockBadge(string nookId, string adapter);
    void ClearNeedsInputSignal(string nookId);
    void ClearDockBadge();
    void DeliverNotification(string id, string title, string body, string nookId);
}

public sealed class NeedsInputSignaler
{
    private readonly ActivityAggregate _activity;
    private readonly INotificationBus _bus;
    private readonly Func<string?> _focusedNookProvider;
    private readonly NotificationPolicyEngine? _policy;
    private readonly HashSet<string> _signaledNooks = new();
    private readonly object _lock = new();

    public NeedsInputSignaler(ActivityAggregate activity, INotificationBus bus, Func<string?> focusedNookProvider, NotificationPolicyEngine? policy = null)
    {
        _activity = activity;
        _bus = bus;
        _focusedNookProvider = focusedNookProvider;
        _policy = policy;
    }

    public void CheckAndSignal(string nookId)
    {
        lock (_lock)
        {
            if (_signaledNooks.Contains(nookId))
                return;
        }

        if (!_activity.NeedsInput(nookId))
            return;

        if (nookId == _focusedNookProvider())
            return;

        var card = _activity.List().FirstOrDefault(c => c.NookId == nookId);
        var adapter = card?.Adapter ?? "unknown";

        lock (_lock)
        {
            if (_signaledNooks.Contains(nookId))
                return;
            _signaledNooks.Add(nookId);
        }

        _bus.BroadcastNeedsInputSignal(nookId, adapter);
        _bus.BroadcastDockBadge(nookId, adapter);

        if (_policy is null)
            return;

        var evaluation = _policy.Evaluate(new NotificationTrigger(NeedsInput: true, AppFocused: false, NookId: nookId, BannerId: null));
        if (evaluation.SuppressOsNotification)
            return;

        _bus.DeliverNotification(nookId, $"{adapter} needs input", "Return to Cove to continue.", nookId);
    }

    public void ClearSignal(string nookId)
    {
        bool removed;
        int remaining;
        lock (_lock)
        {
            removed = _signaledNooks.Remove(nookId);
            remaining = _signaledNooks.Count;
        }

        if (!removed)
            return;

        _policy?.ClearNeedsInput(nookId);
        _bus.ClearNeedsInputSignal(nookId);
        if (remaining == 0)
            _bus.ClearDockBadge();
    }
}
