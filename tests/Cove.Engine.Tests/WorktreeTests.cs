using Cove.Engine.Workspaces;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class WorktreeTests
{
    private sealed class FakeGitRunner : IGitRunner
    {
        public List<string> Calls { get; } = new();

        public Task<GitResult> RunAsync(string workingDir, IReadOnlyList<string> args, CancellationToken cancellationToken = default)
        {
            Calls.Add(string.Join(" ", args));
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
        await using var m = new WorkspaceManager(newId: () => $"id-{++n}", gitRunner: git);
        var parent = await m.CreateWorkspaceAsync("proj", "/repo");

        var wt = await m.CreateWorktreeAsync(parent.Id, "feature", "/repo-wt/feature", newBranch: true);
        Assert.NotNull(wt);
        Assert.True(wt!.IsWorktree);
        Assert.Equal(parent.Id, wt.ParentWorkspaceId);
        Assert.Equal("feature", wt.WorktreeBranch);
        Assert.Equal(parent.CollectionId, wt.CollectionId);
        Assert.Contains(git.Calls, c => c.StartsWith("worktree add", StringComparison.Ordinal));

        var list = m.ListWorktrees(parent.Id);
        Assert.Single(list);
        Assert.Equal(wt.Id, list[0].Id);

        await m.SwitchWorkspaceAsync(wt.Id);
        Assert.Equal(wt.Id, m.Registry.FocusedWorkspaceId);

        var removed = await m.RemoveWorktreeAsync(wt.Id);
        Assert.True(removed);
        Assert.Empty(m.ListWorktrees(parent.Id));
        Assert.Equal(parent.Id, m.Registry.FocusedWorkspaceId);
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
            try { Directory.Delete(repo, true); } catch { }
            try { Directory.Delete(wtPath, true); } catch { }
        }
    }
}
