using Cove.Engine.Bays;
using Cove.Engine.Hooks;
using Cove.Engine.Launch;
using Cove.Engine.Layout;
using Cove.Engine.Pty;
using Cove.Engine.Sessions;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Daemon;

internal sealed class PersistenceCoordinator : IDisposable
{
    private readonly LayoutService _layout;
    private readonly NookRegistry _nooks;
    private readonly string _baysRoot;
    private readonly ILogger _logger;
    private readonly object _dirtyBaysLock = new();
    private readonly HashSet<string> _dirtyBays = new(StringComparer.Ordinal);
    private BayManager? _bays;
    private SessionResumeOrchestrator? _sessions;
    private LaunchOrchestrator? _launcher;
    private HookEventRouter? _hookRouter;
    private Timer? _scrollbackTimer;
    private int _attached;
    private int _disposed;

    public PersistenceCoordinator(
        LayoutService layout,
        NookRegistry nooks,
        string baysRoot,
        ILogger logger)
    {
        _layout = layout;
        _nooks = nooks;
        _baysRoot = baysRoot;
        _logger = logger;
    }

    public string BaysRoot => _baysRoot;

    public void Attach(
        BayManager bays,
        SessionResumeOrchestrator sessions,
        LaunchOrchestrator launcher,
        HookEventRouter hookRouter)
    {
        if (Interlocked.Exchange(ref _attached, 1) != 0)
            throw new InvalidOperationException("persistence coordinator already attached");
        _bays = bays;
        _sessions = sessions;
        _launcher = launcher;
        _hookRouter = hookRouter;
        _layout.OnChanged = PersistActiveBay;
        _layout.OnBayChanged = PersistBay;
        _nooks.OnResized = MarkNookBayDirty;
        _hookRouter.SessionStarted += OnSessionStarted;
    }

    public void StartSnapshotLoop()
    {
        if (Volatile.Read(ref _attached) == 0)
            throw new InvalidOperationException("persistence coordinator is not attached");
        _scrollbackTimer = new Timer(
            _ =>
            {
                PersistAllScrollback();
                FlushDirtyBays();
            },
            null,
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(15));
    }

    public void HandleBayChange(BayChange change)
    {
        if (change.Kind == BayChangeKind.Updated)
            PersistBay(change.BayId);
    }

    public void PersistBayOrder(IReadOnlyList<string> ids)
    {
        var orderPath = Path.Combine(_baysRoot, BayStartup.OrderFileName);
        try
        {
            Directory.CreateDirectory(_baysRoot);
            File.WriteAllLines(orderPath, ids);
        }
        catch (IOException ex)
        {
            _logger.BayOrderPersistenceFailed(orderPath, ex.Message);
        }
    }

    public void PersistActiveBay()
    {
        PersistBay(_layout.ActiveBayId);
    }

    public void MarkNookBayDirty(string nookId)
    {
        foreach (var bayId in _layout.BayIds)
        {
            if (_layout.LeafNookIds(bayId).Contains(nookId, StringComparer.Ordinal))
            {
                lock (_dirtyBaysLock)
                    _dirtyBays.Add(bayId);
                return;
            }
        }
        _logger.NookResizePersistenceSkipped(nookId);
    }

    public void FlushDirtyBays()
    {
        string[] ids;
        lock (_dirtyBaysLock)
        {
            if (_dirtyBays.Count == 0)
                return;
            ids = new string[_dirtyBays.Count];
            _dirtyBays.CopyTo(ids);
            _dirtyBays.Clear();
        }
        foreach (var id in ids)
            PersistBay(id);
    }

    public void PersistBay(string bayId)
    {
        if (Volatile.Read(ref _attached) == 0
            || _bays is null
            || _sessions is null
            || _launcher is null)
        {
            _logger.BayPersistenceBeforeAttach(bayId);
            return;
        }
        if (!_layout.BayIds.Contains(bayId, StringComparer.Ordinal))
        {
            _logger.BayPersistenceMissingLayout(bayId);
            return;
        }
        try
        {
            var actor = _bays.Get(bayId);
            var name = actor?.State.Name ?? bayId;
            var dir = actor?.State.ProjectDir
                ?? _nooks.ProjectDir
                ?? Environment.CurrentDirectory;
            var bayDir = Path.Combine(_baysRoot, bayId);
            var icon = actor?.State.Icon;
            var snapshot = _layout.ToSnapshot(bayId, name, dir) with
            {
                IconKind = icon?.Kind,
                IconValue = icon?.Value,
            };
            var leafIds = new HashSet<string>(
                _layout.LeafNookIds(bayId),
                StringComparer.Ordinal);
            var descriptors = _nooks.Descriptors()
                .Where(descriptor => leafIds.Contains(descriptor.NookId))
                .Select(descriptor =>
                {
                    var sessionId = _sessions.GetState(descriptor.NookId)?.SessionId;
                    var yolo = _launcher.GetOverrides(descriptor.NookId)?.Yolo
                        ?? descriptor.Yolo;
                    return descriptor with
                    {
                        SessionId = string.IsNullOrEmpty(sessionId)
                            ? descriptor.SessionId
                            : sessionId,
                        Yolo = yolo,
                    };
                })
                .ToArray();
            BayPersistence.Save(snapshot, descriptors, bayDir);
        }
        catch (Exception ex)
        {
            _logger.BayPersistenceFailed(ex.Message);
        }
    }

    public void PersistAllScrollback()
    {
        try
        {
            foreach (var bayId in _layout.BayIds)
            {
                var bayDir = Path.Combine(_baysRoot, bayId);
                foreach (var nookId in _layout.LeafNookIds(bayId))
                {
                    var state = _nooks.CaptureTerminalRestoreState(nookId);
                    if (state is not null)
                    {
                        BayPersistence.SaveTerminalRestoreState(nookId, state, bayDir);
                        continue;
                    }
                    var bytes = _nooks.SnapshotRing(nookId);
                    if (bytes.Length > 0)
                        BayPersistence.SaveScrollback(nookId, bytes, bayDir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.ScrollbackPersistenceFailed(ex.Message);
        }
    }

    public void FlushOnShutdown()
    {
        PersistAllScrollback();
        FlushDirtyBays();
    }

    private void OnSessionStarted(string nookId, string adapter, string sessionId)
    {
        if (_sessions is null)
        {
            _logger.SessionPersistenceBeforeAttach(nookId);
            return;
        }
        _sessions.SetSessionId(nookId, adapter, sessionId);
        PersistActiveBay();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _scrollbackTimer?.Dispose();
        if (_hookRouter is not null)
            _hookRouter.SessionStarted -= OnSessionStarted;
        if (_layout.OnChanged == PersistActiveBay)
            _layout.OnChanged = null;
        if (_layout.OnBayChanged == PersistBay)
            _layout.OnBayChanged = null;
        if (_nooks.OnResized == MarkNookBayDirty)
            _nooks.OnResized = null;
    }
}
