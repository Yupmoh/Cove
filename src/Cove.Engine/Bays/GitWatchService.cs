namespace Cove.Engine.Bays;

public sealed class GitWatchService : IDisposable
{
    private readonly Func<string, Task> _onChanged;
    private readonly List<FileSystemWatcher> _watchers = new();
    private CancellationTokenSource? _cts;
    private readonly object _gate = new();
    private string? _repoDir;
    private bool _disposed;

    public GitWatchService(Func<string, Task> onChanged) => _onChanged = onChanged;

    public void Start(string repoDir)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
            return;
        var gitDir = ResolveGitDir(repoDir);
        if (gitDir is null || !Directory.Exists(gitDir))
            return;

        _repoDir = gitDir;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        var targets = new (string Path, bool Recursive)[]
        {
            (gitDir, false),
            (Path.Combine(gitDir, "refs"), true),
            (Path.Combine(gitDir, "worktrees"), true),
        };
        foreach (var (dir, recursive) in targets)
        {
            if (!Directory.Exists(dir))
                continue;
            var fsw = new FileSystemWatcher(dir)
            {
                IncludeSubdirectories = recursive,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            fsw.Changed += (_, e) => HandleChange(e.FullPath);
            fsw.Created += (_, e) => HandleChange(e.FullPath);
            fsw.Deleted += (_, e) => HandleChange(e.FullPath);
            fsw.Renamed += (_, e) => HandleChange(e.FullPath);
            fsw.Error += (_, e) => { };
            _watchers.Add(fsw);
        }
    }

    private void HandleChange(string fullPath)
    {
        if (IsNoise(fullPath))
            return;
        ScheduleDebounce();
    }

    private static bool IsNoise(string fullPath)
    {
        var name = Path.GetFileName(fullPath);
        if (name.EndsWith(".lock", StringComparison.Ordinal))
            return true;
        if (name.EndsWith(".tmp", StringComparison.Ordinal))
            return true;
        return false;
    }

    private void ScheduleDebounce()
    {
        CancellationToken token;
        lock (_gate)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            token = _cts.Token;
        }
        var repo = _repoDir;
        if (repo is null)
            return;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300), token).ConfigureAwait(false);
                if (token.IsCancellationRequested)
                    return;
                await _onChanged(repo).ConfigureAwait(false);
            }
            catch (TaskCanceledException) { }
        });
    }

    private static string? ResolveGitDir(string repoDir)
    {
        if (!Directory.Exists(repoDir))
            return null;
        var dotGit = Path.Combine(repoDir, ".git");
        if (Directory.Exists(dotGit))
        {
            var common = Path.Combine(dotGit, "commondir");
            if (File.Exists(common))
            {
                try
                {
                    var rel = File.ReadAllText(common).Trim();
                    var resolved = Path.GetFullPath(Path.Combine(dotGit, rel));
                    if (Directory.Exists(resolved))
                        return resolved;
                }
                catch { }
            }
            return dotGit;
        }
        if (File.Exists(dotGit))
        {
            try
            {
                var lines = File.ReadAllLines(dotGit);
                foreach (var line in lines)
                    if (line.StartsWith("gitdir:", StringComparison.Ordinal))
                    {
                        var rel = line["gitdir:".Length..].Trim();
                        var resolved = Path.GetFullPath(Path.Combine(repoDir, rel));
                        if (Directory.Exists(resolved))
                            return resolved;
                    }
            }
            catch { }
        }
        if (Directory.Exists(Path.Combine(repoDir, "refs")) && Directory.Exists(Path.Combine(repoDir, "objects")))
            return repoDir;
        return null;
    }

    public void Stop()
    {
        foreach (var fsw in _watchers)
        {
            try { fsw.EnableRaisingEvents = false; fsw.Dispose(); } catch { }
        }
        _watchers.Clear();
        lock (_gate)
        {
            _cts?.Cancel();
            _cts = null;
        }
        _repoDir = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Stop();
    }
}
