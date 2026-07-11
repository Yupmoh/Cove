namespace Cove.Engine.Bays;

public sealed record WorktreeEntry(string Path, string? Branch, string? Head);

public sealed class WorktreeService
{
    private readonly IGitRunner _git;

    public WorktreeService(IGitRunner git) => _git = git;

    public async Task<IReadOnlyList<WorktreeEntry>> ListAsync(string repoDir, CancellationToken cancellationToken = default)
    {
        var result = await _git.RunAsync(repoDir, ["worktree", "list", "--porcelain"], cancellationToken).ConfigureAwait(false);
        return result.Ok ? ParsePorcelain(result.Stdout) : [];
    }

    public static IReadOnlyList<WorktreeEntry> ParsePorcelain(string stdout)
    {
        var entries = new List<WorktreeEntry>();
        string? path = null, branch = null, head = null;
        foreach (var raw in stdout.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                if (path is not null)
                    entries.Add(new WorktreeEntry(path, branch, head));
                path = branch = head = null;
                continue;
            }
            if (line.StartsWith("worktree ", StringComparison.Ordinal))
                path = line["worktree ".Length..];
            else if (line.StartsWith("HEAD ", StringComparison.Ordinal))
                head = line["HEAD ".Length..];
            else if (line.StartsWith("branch ", StringComparison.Ordinal))
                branch = line["branch ".Length..];
        }
        if (path is not null)
            entries.Add(new WorktreeEntry(path, branch, head));
        return entries;
    }

    public Task<GitResult> CreateAsync(string repoDir, string location, string branch, bool newBranch, string? baseRef = null, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "worktree", "add" };
        if (newBranch)
        {
            args.Add("-b");
            args.Add(branch);
            args.Add(location);
            if (!string.IsNullOrEmpty(baseRef))
                args.Add(baseRef);
        }
        else
        {
            args.Add(location);
            args.Add(branch);
        }
        return _git.RunAsync(repoDir, args, cancellationToken);
    }

    public Task<GitResult> RemoveAsync(string repoDir, string worktreePath, bool force, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "worktree", "remove" };
        if (force)
            args.Add("--force");
        args.Add(worktreePath);
        return _git.RunAsync(repoDir, args, cancellationToken);
    }

    public Task<GitResult> PruneAsync(string repoDir, CancellationToken cancellationToken = default)
        => _git.RunAsync(repoDir, ["worktree", "prune"], cancellationToken);

    public async Task<IReadOnlyList<string>> OrphansAsync(string repoDir, IReadOnlyCollection<string> boundPaths, CancellationToken cancellationToken = default)
    {
        var list = await ListAsync(repoDir, cancellationToken).ConfigureAwait(false);
        var bound = new HashSet<string>(boundPaths, StringComparer.Ordinal);
        var result = new List<string>();
        for (int i = 1; i < list.Count; i++)
            if (!bound.Contains(list[i].Path))
                result.Add(list[i].Path);
        return result;
    }
}
