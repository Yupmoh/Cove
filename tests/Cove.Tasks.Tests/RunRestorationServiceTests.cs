using Cove.Tasks.Restart;
using Cove.Tasks.Runs;
using Cove.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Tasks.Tests;

public sealed class RunRestorationServiceTests : TasksTestBase
{
    private static readonly System.TimeSpan LifecycleTimeout = System.TimeSpan.FromSeconds(10);

    private System.Threading.Tasks.Task<TaskService> NewSvcAsync() => CreateTaskServiceAsync("cove-restore-");

    private static async System.Threading.Tasks.Task<(TaskService svc, string cardId, string runId)> SeedActiveRunWithOpenSegmentAsync(TaskService svc)
    {
        var cardId = (await svc.CreateCardAsync("ws1", "restore card", "user:test", "", 1, 2, null)).Id;
        var run = await svc.CreateRunAsync(cardId, "ws1", null);
        await svc.AddRunSegmentAsync(run!.Id, "nook-dead", "adapter-session-xyz");
        return (svc, cardId, run.Id);
    }

    [Fact]
    public async System.Threading.Tasks.Task RestoreOnStartup_FlipsActiveRunsToInterrupted_EndsDeadSegments()
    {
        var svc = await NewSvcAsync();
        var (_, _, runId) = await SeedActiveRunWithOpenSegmentAsync(svc);

        var restoration = new RunRestorationService(svc, NullLogger.Instance);
        var summary = restoration.RestoreOnStartup();

        Assert.Single(summary.RestoredRuns);
        Assert.Equal(RestoredRunOutcome.Interrupted, summary.RestoredRuns[0].Outcome);
        Assert.Equal("adapter-session-xyz", summary.RestoredRuns[0].AdapterSessionId);

        var run = svc.GetRun(runId);
        Assert.Equal("interrupted", run!.State);

        var segments = svc.ListRunSegments(runId);
        Assert.Single(segments);
        Assert.NotNull(segments[0].EndedAt);
    }

    [Fact]
    public async System.Threading.Tasks.Task RestoreOnStartup_NoActiveRuns_ReturnsEmptySummary()
    {
        var svc = await NewSvcAsync();

        var restoration = new RunRestorationService(svc, NullLogger.Instance);
        var summary = restoration.RestoreOnStartup();

        Assert.Empty(summary.RestoredRuns);
    }

    [Fact]
    public async System.Threading.Tasks.Task RestoreOnStartup_AlreadyInterruptedRun_StaysInterrupted()
    {
        var svc = await NewSvcAsync();
        var (_, _, runId) = await SeedActiveRunWithOpenSegmentAsync(svc);
        await svc.TransitionRunAsync(runId, RunState.Interrupted);

        var restoration = new RunRestorationService(svc, NullLogger.Instance);
        var summary = restoration.RestoreOnStartup();

        Assert.Empty(summary.RestoredRuns);
        Assert.Equal("interrupted", svc.GetRun(runId)!.State);
    }

    [Fact]
    public async System.Threading.Tasks.Task ResumeOnRestart_SucceededPath()
    {
        var svc = await NewSvcAsync();
        var (_, _, runId) = await SeedActiveRunWithOpenSegmentAsync(svc);

        var restoration = new RunRestorationService(svc, NullLogger.Instance);
        var summary = restoration.RestoreOnStartup();
        var restored = summary.RestoredRuns[0];

        var result = await restoration.ResumeOnRestartAsync(restored, _ => System.Threading.Tasks.Task.FromResult(true), default);

        Assert.Equal(ResumeOnRestartOutcome.Succeeded, result.Outcome);
        Assert.Equal("succeeded", svc.GetRun(runId)!.State);

        var segments = svc.ListRunSegments(runId);
        Assert.Equal(2, segments.Count);
    }

    [Fact]
    public async System.Threading.Tasks.Task ResumeOnRestart_NoCapturedSession_MarksFailed()
    {
        var svc = await NewSvcAsync();
        var cardId = (await svc.CreateCardAsync("ws1", "no-session", "user:test", "", 1, 2, null)).Id;
        var run = await svc.CreateRunAsync(cardId, "ws1", null);
        await svc.TransitionRunAsync(run!.Id, RunState.Interrupted);

        var restoration = new RunRestorationService(svc, NullLogger.Instance);
        var restored = new RestoredRun(run.Id, cardId, "ws1", null, null, RestoredRunOutcome.Interrupted);

        var result = await restoration.ResumeOnRestartAsync(restored, null, default);

        Assert.Equal(ResumeOnRestartOutcome.FailedNoSession, result.Outcome);
        Assert.Equal("failed", svc.GetRun(run.Id)!.State);
    }

    [Fact]
    public async System.Threading.Tasks.Task ResumeOnRestart_AdapterNotReady_StaysInterrupted()
    {
        var svc = await NewSvcAsync();
        var (_, _, runId) = await SeedActiveRunWithOpenSegmentAsync(svc);

        var restoration = new RunRestorationService(svc, NullLogger.Instance);
        restoration.RestoreOnStartup();
        var restored = new RestoredRun(runId, "", "ws1", "nook-1", "adapter-session-xyz", RestoredRunOutcome.Interrupted);

        var result = await restoration.ResumeOnRestartAsync(restored, _ => System.Threading.Tasks.Task.FromResult(false), default);

        Assert.Equal(ResumeOnRestartOutcome.AdapterNotReady, result.Outcome);
        Assert.Equal("interrupted", svc.GetRun(runId)!.State);
    }

    [Fact]
    public async System.Threading.Tasks.Task ResumeOnRestart_NoAdapterReadyCheck_ProceedsWithoutWaiting()
    {
        var svc = await NewSvcAsync();
        var (_, _, runId) = await SeedActiveRunWithOpenSegmentAsync(svc);

        var restoration = new RunRestorationService(svc, NullLogger.Instance);
        restoration.RestoreOnStartup();
        var restored = new RestoredRun(runId, "", "ws1", "nook-1", "adapter-session-xyz", RestoredRunOutcome.Interrupted);

        var result = await restoration.ResumeOnRestartAsync(restored, null, default);

        Assert.Equal(ResumeOnRestartOutcome.Succeeded, result.Outcome);
        Assert.Equal("succeeded", svc.GetRun(runId)!.State);
    }

    [Fact]
    public async System.Threading.Tasks.Task PendingPrompt_SurvivesRestart_NotSilentlyDropped()
    {
        var fixture = await CreateTaskServiceFixtureAsync("cove-restore-");
        var svc1 = fixture.Service;
        var cardId = (await svc1.CreateCardAsync("ws1", "needs-input card", "user:test", "", 1, 2, null)).Id;
        var run = await svc1.CreateRunAsync(cardId, "ws1", null);
        var runId = run!.Id;
        await svc1.AddRunSegmentAsync(runId, "nook-1", "adapter-session-abc");
        await svc1.SetPendingPromptAsync(runId, "Should I proceed with the refactor?");

        var svc2 = await AsyncTest.CompletesWithinAsync(
            fixture.RestartAsync(),
            LifecycleTimeout,
            "Task service fixture restart did not complete");
        var restoration = new RunRestorationService(svc2, NullLogger.Instance);
        var summary = restoration.RestoreOnStartup();

        Assert.Single(summary.RestoredRuns);
        var restoredRun = svc2.GetRun(runId);
        Assert.Equal("interrupted", restoredRun!.State);
        Assert.Equal("Should I proceed with the refactor?", restoredRun.PendingPrompt);
    }
}
