using System.Text.Json;
using Cove.Engine.Bays;
using Cove.Engine.Diagnostics;
using Cove.Engine.Feedback;
using Cove.Engine.Filesystem;
using Cove.Platform;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class GuiThinClientRouteTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"cove-gui-engine-{Guid.NewGuid():N}");

    [Fact]
    public void DirectoryListingSortsDirectoriesFirstAndCapsEntries()
    {
        Directory.CreateDirectory(Path.Combine(_root, "z-dir"));
        File.WriteAllText(Path.Combine(_root, "a.txt"), "a");
        File.WriteAllText(Path.Combine(_root, "b.txt"), "b");
        var service = new DirectoryListingService(SystemPlatformFileSystem.Instance);

        var result = service.List(_root, 2);

        Assert.Equal(2, result.Entries.Count);
        Assert.Equal("z-dir", result.Entries[0].Name);
        Assert.True(result.Entries[0].IsDir);
        Assert.True(result.Truncated);
        Assert.Null(result.Error);
    }

    [Fact]
    public void DirectoryListingReportsMissingDirectory()
    {
        var service = new DirectoryListingService(SystemPlatformFileSystem.Instance);

        var result = service.List(Path.Combine(_root, "missing"), 400);

        Assert.Empty(result.Entries);
        Assert.False(result.Truncated);
        Assert.Equal("not_found", result.Error);
    }

    [Fact]
    public void GitSummaryParserPreservesLauncherContract()
    {
        const string output = "# branch.head main\n# branch.ab +2 -1\n1 .M N... 100644 100644 100644 abc def src/a.ts\n? untracked.txt\n";

        var result = GitSummaryParser.Parse(output);

        Assert.True(result.Ok);
        Assert.Equal("main", result.Branch);
        Assert.Equal(2, result.Ahead);
        Assert.Equal(1, result.Behind);
        Assert.Equal(2, result.Dirty);
        Assert.Contains(result.Files, file => file.Path == "src/a.ts" && file.Status == "M");
        Assert.Contains(result.Files, file => file.Path == "untracked.txt" && file.Status == "A");
    }

    [Fact]
    public async Task GitSummaryServiceUsesInjectedPlatformProcessRunner()
    {
        Directory.CreateDirectory(_root);
        var process = new StubProcessRunner(new ProcessRunResult(true, false, 0, "# branch.head feature/x\n", "", 1));
        var service = new GitSummaryService(SystemPlatformFileSystem.Instance, process, NullLogger.Instance);

        var result = await service.GetAsync(_root);

        Assert.True(result.Ok);
        Assert.Equal("feature/x", result.Branch);
        Assert.Equal("git", process.Request!.FileName);
        Assert.Equal(_root, process.Request.WorkingDirectory);
        Assert.Equal(["status", "--porcelain=v2", "--branch"], process.Request.Arguments);
        Assert.Equal(TimeSpan.FromSeconds(4), process.Request.Timeout);
    }

    [Fact]
    public void FeedbackStoreContainsUntrustedSlugAndWritesUnderEngineRoot()
    {
        var feedbackRoot = Path.Combine(_root, "feedback");
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 7, 18, 1, 2, 3, TimeSpan.Zero));
        var store = new FeedbackStore(feedbackRoot, SystemPlatformFileSystem.Instance, time, NullLogger.Instance);

        var result = store.Save("{\"kind\":\"cove-ui-feedback\"}", "../../outside");

        Assert.Equal(feedbackRoot, Path.GetDirectoryName(result.Path));
        Assert.Equal("20260718-010203-outside.json", Path.GetFileName(result.Path));
        Assert.Equal("{\"kind\":\"cove-ui-feedback\"}", File.ReadAllText(result.Path));
    }

    [Fact]
    public void PerformanceResultStoreWritesTimestampedAndLatestArtifacts()
    {
        var perfRoot = Path.Combine(_root, "cache", "perf");
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 7, 18, 1, 2, 3, TimeSpan.Zero));
        var store = new PerformanceResultStore(perfRoot, SystemPlatformFileSystem.Instance, time, NullLogger.Instance);

        var result = store.Save("{\"done\":true}", "| a |\n|---|");

        Assert.Equal(perfRoot, result.Directory);
        Assert.Equal("{\"done\":true}", File.ReadAllText(Path.Combine(perfRoot, "latest.json")));
        Assert.Equal("| a |\n|---|", File.ReadAllText(Path.Combine(perfRoot, "latest.md")));
        Assert.Equal("{\"done\":true}", File.ReadAllText(Path.Combine(perfRoot, "perf-20260718-010203.json")));
    }

    [Fact]
    public async Task FeedbackRouteReturnsSourceGeneratedPathResult()
    {
        var feedbackRoot = Path.Combine(_root, "feedback");
        var store = new FeedbackStore(feedbackRoot, SystemPlatformFileSystem.Instance, new ManualTimeProvider(), NullLogger.Instance);
        var parameters = JsonSerializer.SerializeToElement(new FeedbackSaveParams("{}", "report"), CoveJsonContext.Default.FeedbackSaveParams);
        var context = new EngineDispatchContext(new ControlRequest("1", "cove://commands/feedback.save", parameters), feedbackStore: store);

        var response = await FeedbackCommands.Save(context);

        Assert.True(response.Ok);
        var result = response.Data!.Value.Deserialize(CoveJsonContext.Default.FeedbackSaveResult)!;
        Assert.True(File.Exists(result.Path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private sealed class StubProcessRunner(ProcessRunResult result) : IProcessRunner
    {
        public ProcessRunRequest? Request { get; private set; }

        public Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(result);
        }
    }
}
