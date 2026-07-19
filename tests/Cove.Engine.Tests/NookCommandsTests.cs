using System.Text.Json;
using Cove.Engine.Bays;
using Cove.Engine.Nooks;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NookCommandsTests
{

    [Fact]
    public async Task EditorOpen_ReturnsState()
    {
        var prm = JsonDocument.Parse("""{"filePath":"src/file.cs","readOnly":true}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/editor.open", prm));
        Assert.True(resp!.Ok);
    }

    [Fact]
    public async Task EditorSave_RoundTripsState()
    {
        var prm = JsonDocument.Parse("""{"filePath":"src/file.cs","cursor":"10:5","scroll":"0:200","fold":"[1-5]","undo":"step1","readOnly":false}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/editor.save", prm));
        Assert.True(resp!.Ok);
    }

    [Fact]
    public async Task EditorOpen_MissingParams_Fails()
    {
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/editor.open"));
        Assert.False(resp!.Ok);
    }

    [Fact]
    public async Task SearchQuery_ReturnsResult()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-search-route-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "test.txt"), "hello world\n");
        try
        {
            var search = new Cove.Engine.Search.SearchService(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
            var prm = JsonDocument.Parse($$"""{"query":"hello","path":"{{dir.Replace("\\", "\\\\")}}","regex":false,"wholeWord":false,"caseInsensitive":true}""").RootElement.Clone();
            var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/search.query", prm), searchService: search);
            Assert.True(resp!.Ok);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }
    [Fact]
    public async Task SearchReplace_ReplacesInFiles()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-replace-route-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        var filePath = System.IO.Path.Combine(dir, "test.txt");
        System.IO.File.WriteAllText(filePath, "hello world\nhello again\n");
        try
        {
            var search = new Cove.Engine.Search.SearchService(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
            var escapedPath = filePath.Replace("\\", "\\\\");
            var prm = JsonDocument.Parse($$"""{"search":"hello","replacement":"hi","files":["{{escapedPath}}"],"regex":false,"wholeWord":false,"caseInsensitive":true}""").RootElement.Clone();
            var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/search.replace", prm), searchService: search);
            Assert.True(resp!.Ok);
            var content = System.IO.File.ReadAllText(filePath);
            Assert.Equal("hi world\nhi again\n", content);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public async Task SearchSetState_RoundTrips()
    {
        var prm = JsonDocument.Parse("""{"query":"test","regex":true,"wholeWord":false,"caseInsensitive":true,"includeGlobs":["*.ts"],"excludeGlobs":["node_modules"],"scroll":"0:100"}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/search.set-state", prm));
        Assert.True(resp!.Ok);
    }

    [Fact]
    public async Task ScmStatus_ReturnsEntries()
    {
        var (repoDir, git) = SetupTestRepo();
        var prm = JsonDocument.Parse($$"""{"repoRoot":"{{repoDir.Replace("\\", "\\\\")}}"}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/scm.status", prm), gitReadModel: git);
        Assert.True(resp!.Ok);
    }

    [Fact]
    public async Task ScmLog_ReturnsUnpushedAndUnpulledLists()
    {
        var (repoDir, git) = SetupTestRepo();
        var prm = JsonDocument.Parse($$"""{"repoRoot":"{{repoDir.Replace("\\", "\\\\")}}"}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/scm.log", prm), gitReadModel: git);
        Assert.True(resp!.Ok);
        var json = resp.Data!.Value.GetRawText();
        Assert.Contains("unpushed", json);
        Assert.Contains("unpulled", json);
    }

    [Fact]
    public async Task ScmLog_MissingParams_Fails()
    {
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/scm.log"), gitReadModel: null);
        Assert.False(resp!.Ok);
    }

    [Fact]
    public async Task ScmStage_ReturnsOk()
    {
        var (repoDir, git) = SetupTestRepo();
        var prm = JsonDocument.Parse($$"""{"repoRoot":"{{repoDir.Replace("\\", "\\\\")}}","filePath":"test.txt","unstage":false}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/scm.stage", prm), gitReadModel: git);
        Assert.True(resp!.Ok);
    }
    [Fact]
    public async Task ScmDiff_ReturnsPatchContent()
    {
        var (repoDir, git) = SetupTestRepo();
        System.IO.File.WriteAllText(System.IO.Path.Combine(repoDir, "test.txt"), "line1\nline2 modified\n");
        var prm = JsonDocument.Parse($$"""{"repoRoot":"{{repoDir.Replace("\\", "\\\\")}}","filePath":"test.txt","ref":null}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/scm.diff", prm), gitReadModel: git);
        Assert.True(resp!.Ok);
        var json = resp.Data!.Value.GetRawText();
        Assert.Contains("test.txt", json);
        Assert.Contains("line2 modified", json);
        Assert.Contains("diff --git", json);
    }

    [Fact]
    public async Task ScmCommit_ReturnsResult()
    {
        var (repoDir, git) = SetupTestRepo();
        System.IO.File.WriteAllText(System.IO.Path.Combine(repoDir, "test.txt"), "modified\n");
        RunGit(repoDir, "add", "test.txt");
        var prm = JsonDocument.Parse($$"""{"repoRoot":"{{repoDir.Replace("\\", "\\\\")}}","message":"fix: modify","amend":false,"sign":false}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/scm.commit", prm), gitReadModel: git);
        Assert.True(resp!.Ok);
        var json = resp.Data!.Value.GetRawText();
        Assert.Contains("true", json);
    }

    [Fact]
    public async Task ScmBlame_ReturnsResult()
    {
        var (repoDir, git) = SetupTestRepo();
        var prm = JsonDocument.Parse($$"""{"repoRoot":"{{repoDir.Replace("\\", "\\\\")}}","filePath":"test.txt","ref":"HEAD"}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/scm.blame", prm), gitReadModel: git);
        Assert.True(resp!.Ok);
    }

    [Fact]
    public async Task ViewerOpen_ReturnsState()
    {
        var prm = JsonDocument.Parse("""{"filePath":"image.png","ref":"HEAD","contextLines":5}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/viewer.open", prm));
        Assert.True(resp!.Ok);
    }

    [Fact]
    public async Task EditorGetState_ReturnsState()
    {
        var prm = JsonDocument.Parse("""{"filePath":"src/file.cs"}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/editor.get-state", prm));
        Assert.True(resp!.Ok);
    }

    [Fact]
    public async Task SearchGetState_ReturnsState()
    {
        var prm = JsonDocument.Parse("""{"query":""}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/search.get-state", prm));
        Assert.True(resp!.Ok);
    }

    [Fact]
    public async Task ViewerGetState_ReturnsState()
    {
        var prm = JsonDocument.Parse("""{"filePath":"image.png"}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/viewer.get-state", prm));
        Assert.True(resp!.Ok);
    }
    private static (string repoDir, GitReadModel git) SetupTestRepo()
    {
        var repoDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-scm-test-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(repoDir);
        RunGit(repoDir, "init", "-q");
        RunGit(repoDir, "config", "user.email", "test@cove.dev");
        RunGit(repoDir, "config", "user.name", "Test");
        System.IO.File.WriteAllText(System.IO.Path.Combine(repoDir, "test.txt"), "line1\nline2\n");
        RunGit(repoDir, "add", "test.txt");
        RunGit(repoDir, "commit", "-q", "-m", "initial");
        var git = new GitReadModel(new ProcessGitRunner(), NullLogger.Instance);
        return (repoDir, git);
    }

    private static void RunGit(string workingDir, params string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = workingDir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        var p = System.Diagnostics.Process.Start(psi)!;
        p.WaitForExit();
    }
}
