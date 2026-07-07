using Cove.Engine.Activity;

namespace Cove.Engine.Hooks;

public interface INotificationBus
{
    void BroadcastNeedsInputSignal(string paneId, string adapter);
    void BroadcastDockBadge(string paneId, string adapter);
    void ClearNeedsInputSignal(string paneId);
    void ClearDockBadge();
}

public sealed class NeedsInputSignaler
{
    private readonly ActivityAggregate _activity;
    private readonly INotificationBus _bus;
    private readonly Func<string?> _focusedPaneProvider;
    private readonly HashSet<string> _signaledPanes = new();
    private readonly object _lock = new();

    public NeedsInputSignaler(ActivityAggregate activity, INotificationBus bus, Func<string?> focusedPaneProvider)
    {
        _activity = activity;
        _bus = bus;
        _focusedPaneProvider = focusedPaneProvider;
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

        _bus.ClearNeedsInputSignal(paneId);
        if (remaining == 0)
            _bus.ClearDockBadge();
    }
}
