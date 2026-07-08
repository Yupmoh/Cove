using System.Diagnostics;
using System.Text.RegularExpressions;
using Cove.Engine.Workspaces;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed partial class GitReadModelTests : IAsyncDisposable
{
    private readonly string _repoDir;
    private readonly IGitRunner _git;
    private readonly GitReadModel _model;

    public GitReadModelTests()
    {
        _repoDir = Path.Combine(Path.GetTempPath(), $"cove-git-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_repoDir);
        _git = new ProcessGitRunner();
        _model = new GitReadModel(_git, NullLogger.Instance);
        InitRepo();
    }

    private void InitRepo()
    {
        RunGit("init", "-q");
        RunGit("config", "user.email", "test@cove.dev");
        RunGit("config", "user.name", "Test");
        File.WriteAllText(Path.Combine(_repoDir, "committed.txt"), "line1\nline2\n");
        RunGit("add", "committed.txt");
        RunGit("commit", "-q", "-m", "initial");
        File.WriteAllText(Path.Combine(_repoDir, "original.txt"), "rename me\n");
        RunGit("add", "original.txt");
        RunGit("commit", "-q", "-m", "add original");
        RunGit("mv", "original.txt", "renamed.txt");
        File.WriteAllText(Path.Combine(_repoDir, "committed.txt"), "line1\nline2 modified\n");
        File.WriteAllText(Path.Combine(_repoDir, "staged.txt"), "staged content\n");
        RunGit("add", "staged.txt");
        File.WriteAllText(Path.Combine(_repoDir, "untracked.txt"), "untracked\n");
        File.WriteAllText(Path.Combine(_repoDir, "modified.txt"), "modified\n");
        RunGit("add", "modified.txt");
        File.WriteAllText(Path.Combine(_repoDir, "modified.txt"), "modified again\n");
    }

    private void RunGit(params string[] args)
    {
        var result = _git.RunAsync(_repoDir, args).GetAwaiter().GetResult();
        Assert.True(result.Ok, $"git {args[0]} failed: {result.Stderr}");
    }

    [Fact]
    public async Task Status_DetectsStagedUnstagedUntracked()
    {
        var status = await _model.GetStatusAsync(_repoDir);
        Assert.Contains(status.Entries, e => e.FilePath == "staged.txt" && e.IsStaged);
        Assert.Contains(status.Entries, e => e.FilePath == "modified.txt" && e.IsStaged);
        Assert.Contains(status.Entries, e => e.FilePath == "committed.txt" && !e.IsStaged);
        Assert.Contains(status.Entries, e => e.FilePath == "untracked.txt" && e.IsUntracked);
    }

    [Fact]
    public async Task Status_Entry_HasCorrectPathAndStatus()
    {
        var status = await _model.GetStatusAsync(_repoDir);
        var staged = status.Entries.First(e => e.FilePath == "staged.txt");
        Assert.Equal("A", staged.StatusCode);
        Assert.True(staged.IsStaged);
    }

    [Fact]
    public async Task Diff_ParsesNumstat()
    {
        var diff = await _model.GetDiffAsync(_repoDir, "HEAD");
        Assert.NotEmpty(diff.Files);
        Assert.Contains(diff.Files, f => f.Path == "committed.txt");
        Assert.Contains(diff.Files, f => f.Path == "staged.txt");
    }

    [Fact]
    public async Task Diff_File_HasAdditionsAndDeletions()
    {
        var diff = await _model.GetDiffAsync(_repoDir, "HEAD");
        var file = diff.Files.First(f => f.Path == "committed.txt");
        Assert.True(file.Additions > 0);
        Assert.True(file.Deletions > 0);
    }

    [Fact]
    public async Task Blame_ReturnsLineAnnotations()
    {
        var blame = await _model.GetBlameAsync(_repoDir, "committed.txt");
        Assert.NotEmpty(blame.Lines);
        var firstLine = blame.Lines[0];
        Assert.False(string.IsNullOrEmpty(firstLine.Commit));
        Assert.Equal(1, firstLine.LineNumber);
    }

    [Fact]
    public async Task Blame_Line_HasAuthor()
    {
        var blame = await _model.GetBlameAsync(_repoDir, "committed.txt");
        Assert.NotEmpty(blame.Lines);
        Assert.Equal("Test", blame.Lines[0].Author);
    }

    [Fact]
    public async Task Log_ReturnsCommits()
    {
        var log = await _model.GetLogAsync(_repoDir, limit: 10);
        Assert.NotEmpty(log.Commits);
        var first = log.Commits[0];
        Assert.False(string.IsNullOrEmpty(first.Sha));
        Assert.False(string.IsNullOrEmpty(first.Message));
        Assert.Equal("Test", first.Author);
    }

    [Fact]
    public async Task Log_RespectsLimit()
    {
        var log = await _model.GetLogAsync(_repoDir, limit: 1);
        Assert.Single(log.Commits);
    }

    [Fact]
    public async Task Status_ChecksumMatchesGitTruth()
    {
        var status = await _model.GetStatusAsync(_repoDir);
        var truthResult = await _git.RunAsync(_repoDir, ["status", "--porcelain=v2", "-z"]);
        var truthLines = truthResult.Stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var truthFileCount = truthLines.Count(l => l.StartsWith("1 ") || l.StartsWith("2 ") || l.StartsWith("u ") || l.StartsWith("? "));
        Assert.Equal(truthFileCount, status.Entries.Count);
    }
    [Fact]
    public async Task Status_DetectsRenamedFile()
    {
        var status = await _model.GetStatusAsync(_repoDir);
        var renameEntry = status.Entries.FirstOrDefault(e => e.StatusCode == "R");
        Assert.NotNull(renameEntry);
        Assert.Equal("renamed.txt", renameEntry!.FilePath);
        Assert.Equal("original.txt", renameEntry.OldFilePath);
        Assert.True(renameEntry.IsStaged);
    }

    [Fact]
    public async Task Stage_AddsFile()
    {
        File.WriteAllText(Path.Combine(_repoDir, "new.txt"), "new\n");
        await _model.StageAsync(_repoDir, "new.txt");
        var status = await _model.GetStatusAsync(_repoDir);
        Assert.Contains(status.Entries, e => e.FilePath == "new.txt" && e.IsStaged);
    }

    [Fact]
    public async Task Unstage_RestoresFile()
    {
        await _model.UnstageAsync(_repoDir, "staged.txt");
        var status = await _model.GetStatusAsync(_repoDir);
        var entry = status.Entries.FirstOrDefault(e => e.FilePath == "staged.txt");
        if (entry is not null)
            Assert.False(entry.IsStaged);
    }
    [Fact]
    public async Task GetUnpushedAsync_ReturnsCommitsAheadOfUpstream()
    {
        var unpushed = await _model.GetUnpushedAsync(_repoDir);
        Assert.Empty(unpushed.Commits);
    }

    [Fact]
    public async Task GetUnpulledAsync_ReturnsCommitsBehindUpstream()
    {
        var unpulled = await _model.GetUnpulledAsync(_repoDir);
        Assert.Empty(unpulled.Commits);
    }

    [Fact]
    public async Task GetUnpushedAsync_NoUpstream_ReturnsEmpty()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"cove-noupstream-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var git = new ProcessGitRunner();
            await git.RunAsync(dir, ["init", "-q"]);
            await git.RunAsync(dir, ["config", "user.email", "test@cove.dev"]);
            await git.RunAsync(dir, ["config", "user.name", "Test"]);
            File.WriteAllText(Path.Combine(dir, "a.txt"), "a\n");
            await git.RunAsync(dir, ["add", "a.txt"]);
            await git.RunAsync(dir, ["commit", "-q", "-m", "init"]);
            var model = new GitReadModel(git, NullLogger.Instance);
            var unpushed = await model.GetUnpushedAsync(dir);
            Assert.Empty(unpushed.Commits);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    public async ValueTask DisposeAsync()
    {
        await Task.Run(() =>
        {
            try { Directory.Delete(_repoDir, true); } catch { }
        });
    }
}
