using System.IO;
using Microsoft.Extensions.Logging;

namespace Cove.Adapters;

public sealed class AdapterReloadWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly int _debounceMs;
    private readonly Dictionary<string, Timer> _debounceTimers = new();
    private readonly object _lock = new();
    private readonly ILogger? _logger;
    private bool _disposed;

    public event Action<string>? AdapterChanged;
    public event Action? AdaptersChanged;

    public AdapterReloadWatcher(string adaptersRoot, int debounceMs = 200, ILogger? logger = null)
    {
        _debounceMs = debounceMs;
        _logger = logger;
        _watcher = new FileSystemWatcher(adaptersRoot)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
        };
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Renamed += OnRenamed;
        _watcher.Deleted += OnFileEvent;
        _watcher.Error += OnError;
    }

    public void Start() => _watcher.EnableRaisingEvents = true;

    private void OnFileEvent(object sender, FileSystemEventArgs e) => ScheduleReload(e.FullPath);

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        ScheduleReload(e.FullPath);
        if (!string.IsNullOrEmpty(e.OldFullPath) && e.OldFullPath != e.FullPath)
            ScheduleReload(e.OldFullPath);
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        _logger?.AdapterReloadWatcherError(e.GetException().Message);
    }

    private void ScheduleReload(string fullPath)
    {
        var adapterName = ResolveAdapterName(fullPath);
        if (adapterName is null)
            return;

        lock (_lock)
        {
            if (_debounceTimers.TryGetValue(adapterName, out var existing))
                existing.Dispose();

            var timer = new Timer(_ => FireReload(adapterName), null, _debounceMs, Timeout.Infinite);
            _debounceTimers[adapterName] = timer;
        }
    }

    private void FireReload(string adapterName)
    {
        lock (_lock)
        {
            _debounceTimers.Remove(adapterName);
        }
        foreach (var handler in AdapterChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
        {
            try { ((Action<string>)handler).Invoke(adapterName); }
            catch (Exception ex) { _logger?.AdapterReloadHandlerFailed(adapterName, ex.Message); }
        }
        foreach (var handler in AdaptersChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
        {
            try { ((Action)handler).Invoke(); }
            catch (Exception ex) { _logger?.AdapterReloadHandlerFailed(adapterName, ex.Message); }
        }
    }

    private string? ResolveAdapterName(string fullPath)
    {
        var root = _watcher.Path;
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return null;
        var relative = Path.GetRelativePath(root, fullPath);
        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return null;
        var topSegment = segments[0];
        return IsWatchableDir(topSegment) ? topSegment : null;
    }

    public static bool IsWatchableDir(string dirOrPath)
    {
        var name = Path.GetFileName(dirOrPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(name))
            name = dirOrPath;
        return !name.StartsWith(".installing-", StringComparison.Ordinal)
            && !name.StartsWith(".git", StringComparison.Ordinal)
            && !name.StartsWith(".DS_Store", StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= OnFileEvent;
        _watcher.Created -= OnFileEvent;
        _watcher.Renamed -= OnRenamed;
        _watcher.Deleted -= OnFileEvent;
        _watcher.Error -= OnError;
        _watcher.Dispose();
        lock (_lock)
        {
            foreach (var timer in _debounceTimers.Values)
                timer.Dispose();
            _debounceTimers.Clear();
        }
    }
}
