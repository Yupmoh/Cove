using System.IO;
using Microsoft.Extensions.Logging;

namespace Cove.Adapters;

public sealed class SkillIndexWatcher : IDisposable
{
    private readonly SkillIndex _index;
    private readonly ILogger? _logger;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly int _debounceMs;
    private Timer? _debounceTimer;
    private Timer? _recoveryTimer;
    private readonly object _lock = new();
    private bool _disposed;
    private List<(string root, SkillSource source, string? adapterName)> _roots = new();

    public SkillIndexWatcher(SkillIndex index, ILogger? logger = null, int debounceMs = 300)
    {
        _index = index;
        _logger = logger;
        _debounceMs = debounceMs;
    }

    public void Start()
    {
        _roots = _index.GetRoots().ToList();
        foreach (var (root, _, _) in _roots)
        {
            if (Directory.Exists(root))
                WatchRoot(root);
            else
                _logger?.SkillWatchRootMissing(root);
        }

        _recoveryTimer = new Timer(_ => RecoverMissingRoots(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    private void WatchRoot(string root)
    {
        var watchDir = root;
        while (!Directory.Exists(watchDir) && Directory.GetParent(watchDir) is { } parent)
            watchDir = parent.FullName;

        var watcher = new FileSystemWatcher(watchDir)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
        };
        watcher.Changed += OnFileEvent;
        watcher.Created += OnFileEvent;
        watcher.Deleted += OnFileEvent;
        watcher.Renamed += OnFileEvent;
        watcher.Error += OnError;
        watcher.EnableRaisingEvents = true;
        _watchers.Add(watcher);
    }

    private void RecoverMissingRoots()
    {
        foreach (var (root, _, _) in _roots)
        {
            if (Directory.Exists(root) && !_watchers.Any(w => w.Path == root || root.StartsWith(w.Path + Path.DirectorySeparatorChar)))
            {
                WatchRoot(root);
                ScheduleRebuild();
            }
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e) => ScheduleRebuild();
    private void OnRenamed(object sender, RenamedEventArgs e) => ScheduleRebuild();
    private void OnError(object sender, ErrorEventArgs e) => _logger?.SkillWatcherError(e.GetException().Message);

    private void ScheduleRebuild()
    {
        lock (_lock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(_ => DoRebuild(), null, _debounceMs, Timeout.Infinite);
        }
    }

    private void DoRebuild()
    {
        try { _index.Rebuild(); }
        catch (Exception ex) { _logger?.SkillRebuildFailed(ex.Message); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnFileEvent;
            watcher.Created -= OnFileEvent;
            watcher.Deleted -= OnFileEvent;
            watcher.Renamed -= OnFileEvent;
            watcher.Error -= OnError;
            watcher.Dispose();
        }
        _watchers.Clear();
        _recoveryTimer?.Dispose();
        lock (_lock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }
    }
}
