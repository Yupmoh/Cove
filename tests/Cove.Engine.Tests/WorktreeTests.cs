using Cove.Engine.Bays;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class WorktreeTests
{
    private sealed class FakeGitRunner : IGitRunner
    {
        public List<string> Calls { get; } = new();
        private string _porcelain = "";
        public void SetPorcelain(string stdout) => _porcelain = stdout;
        public Task<GitResult> RunAsync(string workingDir, IReadOnlyList<string> args, CancellationToken cancellationToken = default)
        {
            var joined = string.Join(" ", args);
            Calls.Add(joined);
            if (joined.StartsWith("worktree list", StringComparison.Ordinal))
                return Task.FromResult(new GitResult(0, _porcelain, ""));
            return Task.FromResult(new GitResult(0, "", ""));
        }
    }

    [Fact]
    public void ParsePorcelain_ParsesMainAndWorktree()
    {
        var sample = "worktree /repo\nHEAD abc123\nbranch refs/heads/main\n\nworktree /wt/feature\nHEAD def456\nbranch refs/heads/feature\n\n";
        var entries = WorktreeService.ParsePorcelain(sample);
        Assert.Equal(2, entries.Count);
        Assert.Equal("/repo", entries[0].Path);
        Assert.Equal("refs/heads/main", entries[0].Branch);
        Assert.Equal("/wt/feature", entries[1].Path);
        Assert.Equal("refs/heads/feature", entries[1].Branch);
    }

    [Fact]
    public async Task Worktree_Create_List_Remove_ManagerWiring()
    {
        var git = new FakeGitRunner();
        int n = 0;
        await using var m = new BayManager(newId: () => $"id-{++n}", gitRunner: git);
        var parent = await m.CreateBayAsync("proj", "/repo");

        var wt = await m.CreateWorktreeAsync(parent.Id, "feature", "/repo-wt/feature", newBranch: true);
        Assert.NotNull(wt);
        Assert.True(wt!.IsWorktree);
        Assert.Equal(parent.Id, wt.ParentBayId);
        Assert.Equal("feature", wt.WorktreeBranch);
        Assert.Equal(parent.CollectionId, wt.CollectionId);
        Assert.Contains(git.Calls, c => c.StartsWith("worktree add", StringComparison.Ordinal));

        var list = m.ListWorktrees(parent.Id);
        Assert.Single(list);
        Assert.Equal(wt.Id, list[0].Id);

        await m.SwitchBayAsync(wt.Id);
        Assert.Equal(wt.Id, m.Registry.FocusedBayId);

        var removed = await m.RemoveWorktreeAsync(wt.Id);
        Assert.True(removed);
        Assert.Empty(m.ListWorktrees(parent.Id));
        Assert.Equal(parent.Id, m.Registry.FocusedBayId);
        Assert.Contains(git.Calls, c => c.StartsWith("worktree remove", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Worktree_RealGit_RoundTrip()
    {
        var repo = Path.Combine(Path.GetTempPath(), "cove-wt-" + Guid.NewGuid().ToString("N"));
        var wtPath = Path.Combine(Path.GetTempPath(), "cove-wtc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repo);
        var git = new ProcessGitRunner();
        try
        {
            await git.RunAsync(repo, ["init"]);
            await git.RunAsync(repo, ["config", "user.email", "t@example.com"]);
            await git.RunAsync(repo, ["config", "user.name", "t"]);
            File.WriteAllText(Path.Combine(repo, "README.md"), "hi");
            await git.RunAsync(repo, ["add", "."]);
            await git.RunAsync(repo, ["commit", "-m", "init"]);

            var svc = new WorktreeService(git);
            var created = await svc.CreateAsync(repo, wtPath, "feature", newBranch: true);
            Assert.True(created.Ok, created.Stderr);

            var list = await svc.ListAsync(repo);
            Assert.Contains(list, e => Path.GetFileName(e.Path) == Path.GetFileName(wtPath));

            var removed = await svc.RemoveAsync(repo, wtPath, force: true);
            Assert.True(removed.Ok, removed.Stderr);
        }
        finally
        {
            Cove.Testing.TestDirectory.Delete(repo);
            Cove.Testing.TestDirectory.Delete(wtPath);
        }
    }

    [Fact]
    public async Task RefreshWorktreesAsync_Detects_BranchChange_And_Removal()
    {
        var git = new FakeGitRunner();
        int n = 0;
        var changes = new List<BayChange>();
        await using var m = new BayManager(
            emit: c => changes.Add(c),
            newId: () => $"id-{++n}",
            gitRunner: git);
        var parent = await m.CreateBayAsync("proj", "/repo");
        var wt = await m.CreateWorktreeAsync(parent.Id, "feature", "/repo-wt/feature", newBranch: true);
        Assert.NotNull(wt);

        git.SetPorcelain("worktree /repo\nHEAD aaaa\nbranch refs/heads/main\n\nworktree /repo-wt/feature\nHEAD bbbb\nbranch refs/heads/feature\n\n");
        changes.Clear();
        await m.RefreshWorktreesAsync(parent.Id);
        Assert.Empty(changes);

        git.SetPorcelain("worktree /repo\nHEAD aaaa\nbranch refs/heads/main\n\nworktree /repo-wt/feature\nHEAD bbbb\nbranch refs/heads/feature2\n\n");
        changes.Clear();
        await m.RefreshWorktreesAsync(parent.Id);
        Assert.Contains(changes, c => c.Kind == BayChangeKind.Updated && c.BayId == wt!.Id);
        var refreshed = m.ListWorktrees(parent.Id).Single();
        Assert.Equal("feature2", refreshed.WorktreeBranch);

        git.SetPorcelain("worktree /repo\nHEAD aaaa\nbranch refs/heads/main\n\n");
        changes.Clear();
        await m.RefreshWorktreesAsync(parent.Id);
        Assert.Contains(changes, c => c.Kind == BayChangeKind.Deleted && c.BayId == wt!.Id);
        Assert.Empty(m.ListWorktrees(parent.Id));
    }

    [Fact]
    public async Task GitWatch_Reflects_OutOfBand_Worktree_Removal()
    {
        var repo = Path.Combine(Path.GetTempPath(), "cove-watch-" + Guid.NewGuid().ToString("N"));
        var wtPath = Path.Combine(Path.GetTempPath(), "cove-watch-wt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repo);
        var git = new ProcessGitRunner();
        try
        {
            await git.RunAsync(repo, ["init"]);
            await git.RunAsync(repo, ["config", "user.email", "t@example.com"]);
            await git.RunAsync(repo, ["config", "user.name", "t"]);
            File.WriteAllText(Path.Combine(repo, "README.md"), "hi");
            await git.RunAsync(repo, ["add", "."]);
            await git.RunAsync(repo, ["commit", "-m", "init"]);

            var changes = new List<BayChange>();
            var deleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int n = 0;
            await using var m = new BayManager(
                emit: c => { lock (changes) changes.Add(c); if (c.Kind == BayChangeKind.Deleted) deleted.TrySetResult(true); },
                newId: () => $"id-{++n}",
                gitRunner: git);
            var parent = await m.CreateBayAsync("proj", repo);
            var wt = await m.CreateWorktreeAsync(parent.Id, "feature", wtPath, newBranch: true);
            Assert.NotNull(wt);

            await m.WatchWorktreeRepoAsync(parent.Id);
            try
            {
                await m.RefreshWorktreesAsync(parent.Id);
                Assert.Single(m.ListWorktrees(parent.Id));

                var removed = await git.RunAsync(repo, ["worktree", "remove", "--force", wtPath]);
                Assert.True(removed.Ok, removed.Stderr);

                var winner = await Task.WhenAny(deleted.Task, Task.Delay(TimeSpan.FromSeconds(10)));
                Assert.Same(deleted.Task, winner);
                Assert.Empty(m.ListWorktrees(parent.Id));
            }
            finally
            {
                await m.UnwatchWorktreeRepoAsync(parent.Id);
            }
        }
        finally
        {
            Cove.Testing.TestDirectory.Delete(repo);
            Cove.Testing.TestDirectory.Delete(wtPath);
        }
    }

    [Fact]
    public void PathRealpath_Resolves_Deep_Symlink_Not_First_Component()
    {
        var tmp = Path.GetTempPath();
        var real = Path.Combine(tmp, "cove-real-" + Guid.NewGuid().ToString("N"));
        var linkParent = Path.Combine(tmp, "cove-linkparent-" + Guid.NewGuid().ToString("N"));
        var link = Path.Combine(linkParent, "link");
        Directory.CreateDirectory(real);
        Directory.CreateDirectory(linkParent);
        Directory.CreateSymbolicLink(link, real);
        try
        {
            var deep = Path.Combine(link, "sub");
            Directory.CreateDirectory(Path.Combine(real, "sub"));
            var normalized = PathRealpath.Normalize(deep);
            var expected = PathRealpath.Normalize(Path.Combine(real, "sub"));
            Assert.Equal(expected, normalized);
            Assert.DoesNotContain("link", normalized);
        }
        finally
        {
            Cove.Testing.TestDirectory.Delete(linkParent);
            Cove.Testing.TestDirectory.Delete(real);
        }
    }
    [Fact]
    public async Task AdoptWorktree_BindsOrphan_AsChildBay()
    {
        var repo = Path.Combine(Path.GetTempPath(), "cove-adopt-" + Guid.NewGuid().ToString("N"));
        var wtPath = Path.Combine(Path.GetTempPath(), "cove-adopt-wt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repo);
        var git = new ProcessGitRunner();
        try
        {
            await git.RunAsync(repo, ["init"]);
            await git.RunAsync(repo, ["config", "user.email", "t@example.com"]);
            await git.RunAsync(repo, ["config", "user.name", "t"]);
            File.WriteAllText(Path.Combine(repo, "README.md"), "hi");
            await git.RunAsync(repo, ["add", "."]);
            await git.RunAsync(repo, ["commit", "-m", "init"]);
            await git.RunAsync(repo, ["worktree", "add", wtPath, "-b", "feature"]);

            int n = 0;
            var changes = new System.Collections.Generic.List<BayChange>();
            await using var m = new BayManager(
                newId: () => $"id-{++n}",
                gitRunner: git,
                emit: c => changes.Add(c));
            var parent = await m.CreateBayAsync("proj", repo);

            var adopted = await m.AdoptWorktreeAsync(parent.Id, wtPath, "feature");
            Assert.NotNull(adopted);
            Assert.True(adopted!.IsWorktree);
            Assert.Equal(parent.Id, adopted.ParentBayId);
            Assert.Equal("feature", adopted.WorktreeBranch);
            Assert.Equal(wtPath, adopted.ProjectDir);

            var orphans = await m.WorktreeOrphansAsync(parent.Id);
            Assert.DoesNotContain(wtPath, orphans);
        }
        finally
        {
            Cove.Testing.TestDirectory.Delete(repo);
            Cove.Testing.TestDirectory.Delete(wtPath);
        }
    }
}
