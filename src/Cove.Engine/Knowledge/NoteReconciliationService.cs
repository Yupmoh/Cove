using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed record FileReconcileEvent(string FilePath, string BayId, FileChangeKind ChangeKind);

public enum FileChangeKind { Created, Changed, Deleted, Renamed }

public sealed class NoteReconciliationService : IDisposable
{
    private readonly NoteFileStore _noteStore;
    private readonly ILogger _logger;
    private readonly System.Threading.Timer _debounceTimer;
    private readonly object _pendingLock = new();
    private readonly object _callbackLock = new();
    private readonly System.Collections.Generic.Dictionary<string, FileReconcileEvent> _pending = new();
    private readonly System.TimeSpan _debounceDelay;
    private FileSystemWatcher? _watcher;
    private string _bayId = "";
    private bool _watching;
    private bool _disposed;
    private bool _disposeCompleted;

    public event System.EventHandler<FileReconcileEvent>? ReconcileNeeded;

    public NoteReconciliationService(
        NoteFileStore noteStore,
        ILogger logger,
        System.TimeSpan? debounceDelay = null)
    {
        _noteStore = noteStore;
        _logger = logger;
        _debounceDelay = debounceDelay ?? System.TimeSpan.FromMilliseconds(300);
        _debounceTimer = new System.Threading.Timer(FlushPending, null, System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
    }

    public void StartWatching(string notesRoot, string bayId)
    {
        _bayId = bayId;
        ReconcileIndex();
        if (!System.IO.Directory.Exists(notesRoot))
        {
            _logger.LogWarning("reconcile: notes root {root} does not exist, not watching", notesRoot);
            return;
        }

        var watcher = new FileSystemWatcher(notesRoot)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.Size,
        };

        watcher.Created += (_, e) => OnFileEvent(e.FullPath, FileChangeKind.Created);
        watcher.Changed += (_, e) => OnFileEvent(e.FullPath, FileChangeKind.Changed);
        watcher.Deleted += (_, e) => OnFileEvent(e.FullPath, FileChangeKind.Deleted);
        watcher.Renamed += (_, e) => OnFileEvent(e.FullPath, FileChangeKind.Renamed);
        watcher.Error += (_, e) => _logger.LogWarning("reconcile: file watcher error: {error}", e.GetException().Message);
        lock (_pendingLock)
        {
            if (_disposed)
            {
                watcher.Dispose();
                throw new ObjectDisposedException(nameof(NoteReconciliationService));
            }
            _watching = true;
            _watcher = watcher;
            watcher.EnableRaisingEvents = true;
        }

        _logger.LogWarning("reconcile: watching {root} for bay {ws}", notesRoot, bayId);
    }

    private void OnFileEvent(string filePath, FileChangeKind kind)
    {
        if (filePath.Contains(".git") || filePath.EndsWith(".tmp") || filePath.Contains(".cove-tmp"))
            return;

        lock (_pendingLock)
        {
            if (!_watching || _disposed)
                return;
            _pending[filePath] = new FileReconcileEvent(filePath, _bayId, kind);
            _debounceTimer.Change(_debounceDelay, System.Threading.Timeout.InfiniteTimeSpan);
        }
    }

    public void StopWatching()
    {
        FileSystemWatcher? watcher;
        lock (_pendingLock)
        {
            if (_disposed)
                return;
            _watching = false;
            watcher = _watcher;
            _watcher = null;
            _debounceTimer.Change(System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
        }
        if (watcher is not null)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        FlushPending(null);
    }

    private void FlushPending(object? state)
    {
        lock (_callbackLock)
        {
            System.Collections.Generic.List<FileReconcileEvent> toFire;
            lock (_pendingLock)
            {
                if (_disposed)
                {
                    _pending.Clear();
                    return;
                }

                toFire = new System.Collections.Generic.List<FileReconcileEvent>(_pending.Values);
                _pending.Clear();
            }

            if (toFire.Count > 0)
                ReconcileIndex();

            foreach (var evt in toFire)
            {
                lock (_pendingLock)
                {
                    if (_disposed)
                        return;
                }

                _logger.LogWarning("reconcile: {kind} {file} in bay {ws}", evt.ChangeKind, evt.FilePath, evt.BayId);
                var handlers = ReconcileNeeded?.GetInvocationList();
                if (handlers is null)
                    continue;

                foreach (var handler in handlers)
                {
                    lock (_pendingLock)
                    {
                        if (_disposed)
                            return;
                    }

                    ((System.EventHandler<FileReconcileEvent>)handler)(this, evt);
                }
            }
        }
    }

    private void ReconcileIndex()
    {
        try
        {
            _noteStore.RebuildIndexFromDisk();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "reconcile: note index rebuild failed: {error}",
                exception.Message);
        }
    }

    public void Dispose()
    {
        FileSystemWatcher? watcher;
        lock (_pendingLock)
        {
            if (_disposed)
            {
                if (System.Threading.Monitor.IsEntered(_callbackLock))
                    return;

                while (!_disposeCompleted)
                    System.Threading.Monitor.Wait(_pendingLock);
                return;
            }

            _disposed = true;
            _watching = false;
            watcher = _watcher;
            _watcher = null;
            _debounceTimer.Change(System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
        }

        System.Collections.Generic.List<Exception>? failures = null;
        try
        {
            if (watcher is not null)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                }
                catch (Exception exception)
                {
                    (failures ??= new System.Collections.Generic.List<Exception>()).Add(exception);
                }

                try
                {
                    watcher.Dispose();
                }
                catch (Exception exception)
                {
                    (failures ??= new System.Collections.Generic.List<Exception>()).Add(exception);
                }
            }

            try
            {
                lock (_callbackLock)
                    _debounceTimer.Dispose();
            }
            catch (Exception exception)
            {
                (failures ??= new System.Collections.Generic.List<Exception>()).Add(exception);
            }
        }
        finally
        {
            lock (_pendingLock)
            {
                _pending.Clear();
                _disposeCompleted = true;
                System.Threading.Monitor.PulseAll(_pendingLock);
            }
        }

        if (failures is { Count: 1 })
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures is not null)
            throw new AggregateException(failures);
    }
}
