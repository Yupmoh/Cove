using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Engine.Restart;
using Cove.Protocol;

namespace Cove.Engine.Daemon;

internal sealed class EngineEventRouter
{
    private readonly CancellationToken _shutdownToken;
    private readonly object _guiLock = new();
    private readonly List<FrameConnection> _guiConnections = new();
    private RestorationSummaryEvent? _restorationSummary;
    private long _workspaceRevision;

    public EngineEventRouter(CancellationToken shutdownToken)
    {
        _shutdownToken = shutdownToken;
    }

    public RestorationSummaryEvent? RestorationSummary => Volatile.Read(ref _restorationSummary);
    public long WorkspaceRevision => Interlocked.Read(ref _workspaceRevision);

    public void SetRestorationSummary(RestorationSummaryEvent summary)
    {
        Volatile.Write(ref _restorationSummary, summary);
    }

    public void RegisterGui(FrameConnection connection)
    {
        lock (_guiLock)
            _guiConnections.Add(connection);
        var summary = RestorationSummary;
        if (summary is not null)
            Broadcast("restore.summary", summary, RestorationSummaryJsonContext.Default.RestorationSummaryEvent);
    }

    public void UnregisterGui(FrameConnection connection)
    {
        lock (_guiLock)
            _guiConnections.Remove(connection);
    }

    public bool TryForwardFocus(CancellationToken cancellationToken)
    {
        FrameConnection? gui;
        lock (_guiLock)
            gui = _guiConnections.Count > 0 ? _guiConnections[0] : null;
        if (gui is null)
            return false;
        using var document = JsonDocument.Parse("{}");
        var frame = ControlCodec.Encode(new ControlEvent("window.focus", document.RootElement.Clone()));
        _ = gui.WriteFrameAsync(FrameType.Event, 0, frame, cancellationToken);
        return true;
    }

    public void PublishMutation(string uri)
    {
        if (IsMutatingVerb(uri))
        {
            Broadcast(
                "state.changed",
                new StateChangedEvent(uri),
                CoveJsonContext.Default.StateChangedEvent);
            var taskChannel = ResolveTaskEventChannel(uri);
            if (taskChannel is not null)
            {
                Broadcast(
                    taskChannel,
                    new StateChangedEvent(uri),
                    CoveJsonContext.Default.StateChangedEvent);
            }
        }
        if (IsWorkspaceMutatingVerb(uri))
        {
            var revision = Interlocked.Increment(ref _workspaceRevision);
            Broadcast(
                "workspace.changed",
                new WorkspaceChangedEvent(revision, uri),
                CoveJsonContext.Default.WorkspaceChangedEvent);
        }
    }

    public void Broadcast<T>(string channel, T payload, JsonTypeInfo<T> typeInfo)
    {
        FrameConnection[] guis;
        lock (_guiLock)
            guis = _guiConnections.ToArray();
        if (guis.Length == 0)
            return;
        var element = JsonSerializer.SerializeToElement(payload, typeInfo);
        var frame = ControlCodec.Encode(new ControlEvent(channel, element));
        foreach (var gui in guis)
            _ = gui.WriteFrameAsync(FrameType.Event, 0, frame, _shutdownToken);
    }

    public void BroadcastDictation(string channel, JsonElement payload)
    {
        FrameConnection[] guis;
        lock (_guiLock)
            guis = _guiConnections.ToArray();
        if (guis.Length == 0)
            return;
        var frame = ControlCodec.Encode(new ControlEvent(channel, payload));
        foreach (var gui in guis)
            _ = gui.WriteFrameAsync(FrameType.Event, 0, frame, _shutdownToken);
    }

    private static bool IsMutatingVerb(string uri)
    {
        return uri.StartsWith("cove://commands/bay.", StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/shore.", StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/wing.", StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/collection.", StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/resident.", StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/worktree.", StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/bay-command.", StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/task.", StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/run.", StringComparison.Ordinal)
            || uri == "cove://commands/activity.acknowledge";
    }

    internal static bool IsWorkspaceMutatingVerb(string uri)
    {
        return uri == "cove://commands/layout.mutate"
            || uri is "cove://commands/nook.spawn"
                or "cove://commands/nook.kill"
                or "cove://commands/nook.rename"
                or "cove://commands/agent.launch"
            || IsVisibleMutationGroup(uri, "cove://commands/bay.", "list")
            || IsVisibleMutationGroup(uri, "cove://commands/shore.", "list")
            || IsVisibleMutationGroup(uri, "cove://commands/wing.", "list");
    }

    private static bool IsVisibleMutationGroup(string uri, string prefix, string readVerb)
    {
        return uri.StartsWith(prefix, StringComparison.Ordinal)
            && !uri.Equals(prefix + readVerb, StringComparison.Ordinal);
    }

    private static string? ResolveTaskEventChannel(string uri)
    {
        if (uri.StartsWith("cove://commands/task.", StringComparison.Ordinal))
        {
            if (uri.Contains("status.", StringComparison.Ordinal)
                || uri.Contains("label.", StringComparison.Ordinal))
            {
                return "task.board.invalidated";
            }
            return "task.card.changed";
        }
        if (uri.StartsWith("cove://commands/run.", StringComparison.Ordinal))
            return "task.run.changed";
        return null;
    }
}

internal sealed class DaemonNotificationBus : Cove.Engine.Hooks.INotificationBus
{
    private readonly EngineEventRouter _events;

    public DaemonNotificationBus(EngineEventRouter events)
    {
        _events = events;
    }

    public void BroadcastNeedsInputSignal(string nookId, string adapter)
    {
        _events.Broadcast(
            "needs-input.signal",
            new NeedsInputSignalDto(nookId, adapter),
            CoveJsonContext.Default.NeedsInputSignalDto);
    }

    public void BroadcastDockBadge(string nookId, string adapter)
    {
        _events.Broadcast(
            "dock.badge",
            new NeedsInputSignalDto(nookId, adapter),
            CoveJsonContext.Default.NeedsInputSignalDto);
    }

    public void ClearNeedsInputSignal(string nookId)
    {
        _events.Broadcast(
            "needs-input.clear",
            new NeedsInputSignalDto(nookId, ""),
            CoveJsonContext.Default.NeedsInputSignalDto);
    }

    public void ClearDockBadge()
    {
        _events.Broadcast(
            "dock.badge.clear",
            new NeedsInputSignalDto("", ""),
            CoveJsonContext.Default.NeedsInputSignalDto);
    }

    public void DeliverNotification(string id, string title, string body, string nookId)
    {
        _events.Broadcast(
            "notification.deliver",
            new NotificationDeliverDto(id, title, body, nookId),
            CoveJsonContext.Default.NotificationDeliverDto);
    }
}
