using Cove.Engine.Knowledge;
using Cove.Engine.Bays;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class GitCommitIngesterTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-git-" + System.Guid.NewGuid().ToString("N"));

    private static async Task<string> InitRepoAsync(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        var psi = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        psi.ArgumentList.Add("init");
        using var p = System.Diagnostics.Process.Start(psi)!;
        await p.WaitForExitAsync();
        psi = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        psi.ArgumentList.Add("config"); psi.ArgumentList.Add("user.email"); psi.ArgumentList.Add("cove@cove.local");
        using var p2 = System.Diagnostics.Process.Start(psi)!; await p2.WaitForExitAsync();
        psi = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        psi.ArgumentList.Add("config"); psi.ArgumentList.Add("user.name"); psi.ArgumentList.Add("Cove Test");
        using var p3 = System.Diagnostics.Process.Start(psi)!; await p3.WaitForExitAsync();
        return dir;
    }

    private static async Task CommitAsync(string dir, string message)
    {
        var filePath = System.IO.Path.Combine(dir, "f.txt");
        await System.IO.File.WriteAllTextAsync(filePath, System.Guid.NewGuid().ToString());
        await RunGitAsync(dir, ["add", "f.txt"]);
        await RunGitAsync(dir, ["commit", "-m", message]);
    }

    private static async Task RunGitAsync(string dir, string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = System.Diagnostics.Process.Start(psi)!;
        await p.WaitForExitAsync();
    }

    [Fact]
    public async Task FreshCommit_AppearsOnceInTimeline()
    {
        var dir = NewDir();
        try
        {
            await InitRepoAsync(dir);
            await CommitAsync(dir, "fix: resolve race in scheduler");

            var (_, store) = NewStore(dir);
            var ingester = new GitCommitIngester(store, new ProcessGitRunner(), NullLogger.Instance);
            await ingester.IngestAsync(dir, "ws1");

            var list = store.ListByBay("ws1");
            Assert.Single(list);
            Assert.Equal("git.commit", list[0].Kind);
            Assert.Contains("resolve race", list[0].Summary);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task ReRunningIngester_AddsNothing()
    {
        var dir = NewDir();
        try
        {
            await InitRepoAsync(dir);
            await CommitAsync(dir, "feat: add timeline store");

            var (_, store) = NewStore(dir);
            var ingester = new GitCommitIngester(store, new ProcessGitRunner(), NullLogger.Instance);
            await ingester.IngestAsync(dir, "ws1");
            await ingester.IngestAsync(dir, "ws1");

            var list = store.ListByBay("ws1");
            Assert.Single(list);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task NonGitRepo_SkipsSilently()
    {
        var dir = NewDir();
        try
        {
            System.IO.Directory.CreateDirectory(dir);
            var (_, store) = NewStore(dir);
            var ingester = new GitCommitIngester(store, new ProcessGitRunner(), NullLogger.Instance);
            var count = await ingester.IngestAsync(dir, "ws1");
            Assert.Equal(0, count);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    private static (string dir, TimelineStore store) NewStore(string dir)
    {
        var kernel = new KnowledgePersistenceKernel(dir, NullLogger.Instance);
        kernel.EnsureAllSchemas();
        return (dir, new TimelineStore(dir, NullLogger.Instance));
    }
}
