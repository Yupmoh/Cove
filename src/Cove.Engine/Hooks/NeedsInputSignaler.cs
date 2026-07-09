using Cove.Engine.Activity;
using Cove.Engine.Notifications;

namespace Cove.Engine.Hooks;

public interface INotificationBus
{
    void BroadcastNeedsInputSignal(string paneId, string adapter);
    void BroadcastDockBadge(string paneId, string adapter);
    void ClearNeedsInputSignal(string paneId);
    void ClearDockBadge();
    void DeliverNotification(string id, string title, string body, string paneId);
}

public sealed class NeedsInputSignaler
{
    private readonly ActivityAggregate _activity;
    private readonly INotificationBus _bus;
    private readonly Func<string?> _focusedPaneProvider;
    private readonly NotificationPolicyEngine? _policy;
    private readonly HashSet<string> _signaledPanes = new();
    private readonly object _lock = new();

    public NeedsInputSignaler(ActivityAggregate activity, INotificationBus bus, Func<string?> focusedPaneProvider, NotificationPolicyEngine? policy = null)
    {
        _activity = activity;
        _bus = bus;
        _focusedPaneProvider = focusedPaneProvider;
        _policy = policy;
    }

    public void CheckAndSignal(string paneId)
    {
        lock (_lock)
        {
            if (_signaledPanes.Contains(paneId))
                return;
        }

        if (!_activity.NeedsInput(paneId))
            return;

        if (paneId == _focusedPaneProvider())
            return;

        var card = _activity.List().FirstOrDefault(c => c.PaneId == paneId);
        var adapter = card?.Adapter ?? "unknown";

        lock (_lock)
        {
            if (_signaledPanes.Contains(paneId))
                return;
            _signaledPanes.Add(paneId);
        }

        _bus.BroadcastNeedsInputSignal(paneId, adapter);
        _bus.BroadcastDockBadge(paneId, adapter);

        if (_policy is null)
            return;

        var evaluation = _policy.Evaluate(new NotificationTrigger(NeedsInput: true, AppFocused: false, PaneId: paneId, BannerId: null));
        if (evaluation.SuppressOsNotification)
            return;

        _bus.DeliverNotification(paneId, $"{adapter} needs input", "Return to Cove to continue.", paneId);
    }

    public void ClearSignal(string paneId)
    {
        bool removed;
        int remaining;
        lock (_lock)
        {
            removed = _signaledPanes.Remove(paneId);
            remaining = _signaledPanes.Count;
        }

        if (!removed)
            return;

        _policy?.ClearNeedsInput(paneId);
        _bus.ClearNeedsInputSignal(paneId);
        if (remaining == 0)
            _bus.ClearDockBadge();
    }
}
