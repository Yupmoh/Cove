using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed record FileReconcileEvent(string FilePath, string WorkspaceId, FileChangeKind ChangeKind);

public enum FileChangeKind { Created, Changed, Deleted, Renamed }

public sealed class NoteReconciliationService : IDisposable
{
    private readonly ILogger _logger;
    private readonly System.Threading.Timer _debounceTimer;
    private readonly object _pendingLock = new();
    private readonly System.Collections.Generic.Dictionary<string, FileReconcileEvent> _pending = new();
    private readonly System.TimeSpan _debounceDelay;
    private FileSystemWatcher? _watcher;
    private string _workspaceId = "";

    public event System.EventHandler<FileReconcileEvent>? ReconcileNeeded;

    public NoteReconciliationService(ILogger logger, System.TimeSpan? debounceDelay = null)
    {
        _logger = logger;
        _debounceDelay = debounceDelay ?? System.TimeSpan.FromMilliseconds(300);
        _debounceTimer = new System.Threading.Timer(FlushPending, null, System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
    }

    public void StartWatching(string notesRoot, string workspaceId)
    {
        _workspaceId = workspaceId;
        if (!System.IO.Directory.Exists(notesRoot))
        {
            _logger.LogWarning("reconcile: notes root {root} does not exist, not watching", notesRoot);
            return;
        }

        _watcher = new FileSystemWatcher(notesRoot)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };

        _watcher.Created += (_, e) => OnFileEvent(e.FullPath, FileChangeKind.Created);
        _watcher.Changed += (_, e) => OnFileEvent(e.FullPath, FileChangeKind.Changed);
        _watcher.Deleted += (_, e) => OnFileEvent(e.FullPath, FileChangeKind.Deleted);
        _watcher.Renamed += (_, e) => OnFileEvent(e.FullPath, FileChangeKind.Renamed);
        _watcher.Error += (_, e) => _logger.LogWarning("reconcile: file watcher error: {error}", e.GetException().Message);

        _logger.LogWarning("reconcile: watching {root} for workspace {ws}", notesRoot, workspaceId);
    }

    private void OnFileEvent(string filePath, FileChangeKind kind)
    {
        if (filePath.Contains(".git") || filePath.EndsWith(".tmp") || filePath.Contains(".cove-tmp"))
            return;

        var evt = new FileReconcileEvent(filePath, _workspaceId, kind);
        lock (_pendingLock)
        {
            _pending[filePath] = evt;
        }
        _debounceTimer.Change(_debounceDelay, System.Threading.Timeout.InfiniteTimeSpan);
    }

    private void FlushPending(object? state)
    {
        System.Collections.Generic.List<FileReconcileEvent> toFire;
        lock (_pendingLock)
        {
            toFire = new System.Collections.Generic.List<FileReconcileEvent>(_pending.Values);
            _pending.Clear();
        }

        foreach (var evt in toFire)
        {
            _logger.LogWarning("reconcile: {kind} {file} in workspace {ws}", evt.ChangeKind, evt.FilePath, evt.WorkspaceId);
            ReconcileNeeded?.Invoke(this, evt);
        }
    }

    public void Dispose()
    {
        _debounceTimer?.Dispose();
        _watcher?.Dispose();
    }
}
