using Cove.Persistence;
using Cove.Tasks.Contracts;
using Cove.Tasks.Dispatch;
using Cove.Tasks.Runs;
using Cove.Tasks.Store;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Tasks.Tests;

public sealed class ResumeSagaTests
{
    private static string NewDb() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-resume-" + System.Guid.NewGuid().ToString("N") + ".db");

    private static async System.Threading.Tasks.Task<TaskService> NewSvcAsync()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-resume-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var svc = new TaskService(dir, NullLogger.Instance);
        await svc.StartAsync();
        return svc;
    }

    private sealed class FakeProfileResolver : ILaunchProfileResolver
    {
        public LaunchProfileResolution? Result { get; set; } = new("claude", "default", "claude --resume", new Dictionary<string, string>());
        public LaunchProfileResolution? ResolveTaskProfile(string ws, string cardId) => Result;
    }

    private sealed class FakeNookHost : INookHost
    {
        public bool ShouldFailEnv { get; set; }
        public Dictionary<string, Dictionary<string, string>> InjectedEnvs { get; } = new();
        public Dictionary<string, string> BoundCards { get; } = new();
        public NookCreationResult? CreateNook(string? adapter, int cols, int rows) => new("nook-1");
        public bool InjectEnv(string nookId, IReadOnlyDictionary<string, string> env)
        {
            if (ShouldFailEnv) return false;
            InjectedEnvs[nookId] = new Dictionary<string, string>(env);
            return true;
        }
        public bool BindTaskCard(string nookId, string cardId) { BoundCards[nookId] = cardId; return true; }
    }

    private sealed class FakeResumeLauncher : IAdapterResumeLauncher
    {
        public bool ShouldFail { get; set; }
        public string? Error { get; set; }
        public int ResumeCalls { get; private set; }
        public string? LastPriorSessionId { get; private set; }
        public string? LastAdapter { get; private set; }
        public AdapterResumeResult Resume(string nookId, string adapter, string resolvedCommand, string priorAdapterSessionId, IReadOnlyDictionary<string, string> env)
        {
            ResumeCalls++;
            LastPriorSessionId = priorAdapterSessionId;
            LastAdapter = adapter;
            return ShouldFail ? new AdapterResumeResult("", false, Error ?? "resume failed") : new AdapterResumeResult($"resumed-session-{ResumeCalls}", true, null);
        }
    }

    private static async System.Threading.Tasks.Task<(TaskService svc, string cardId, string runId)> SeedRunWithSegmentAsync(TaskService svc)
    {
        var cardId = (await svc.CreateCardAsync("ws1", "resume card", "user:test", "do the thing", 1, 2, null)).Id;
        var run = await svc.CreateRunAsync(cardId, "ws1", null);
        await svc.AddRunSegmentAsync(run!.Id, "nook-original", "adapter-session-abc");
        return (svc, cardId, run.Id);
    }

    [Fact]
    public async System.Threading.Tasks.Task HappyPath_ResumesInterruptedRunIntoNewSegment()
    {
        var svc = await NewSvcAsync();
        var (_, _, runId) = await SeedRunWithSegmentAsync(svc);
        await svc.TransitionRunAsync(runId, RunState.Interrupted);

        var resolver = new FakeProfileResolver();
        var nookHost = new FakeNookHost();
        var resumeLauncher = new FakeResumeLauncher();
        var saga = new ResumeSaga(svc, resolver, nookHost, resumeLauncher, NullLogger.Instance);

        var result = await saga.ResumeAsync(runId, "nook-new", null);

        Assert.True(result.Success, result.Error);
        Assert.Equal(ResumeOutcome.Resumed, result.Outcome);
        Assert.NotNull(result.NewSegmentId);
        Assert.Equal(1, resumeLauncher.ResumeCalls);
        Assert.Equal("adapter-session-abc", resumeLauncher.LastPriorSessionId);
        Assert.Equal("claude", resumeLauncher.LastAdapter);

        var segments = svc.ListRunSegments(runId);
        Assert.Equal(2, segments.Count);
        Assert.Equal("nook-new", segments[1].NookId);
        Assert.Equal("resumed-session-1", segments[1].AdapterSessionId);

        var run = svc.GetRun(runId);
        Assert.Equal("active", run!.State);

        Assert.Contains("COVE_TASK_ID", nookHost.InjectedEnvs["nook-new"].Keys);
        Assert.Contains("COVE_TASK_RUN_ID", nookHost.InjectedEnvs["nook-new"].Keys);
    }

    [Fact]
    public async System.Threading.Tasks.Task RunNotFound_ReturnsError()
    {
        var svc = await NewSvcAsync();
        var saga = new ResumeSaga(svc, new FakeProfileResolver(), new FakeNookHost(), new FakeResumeLauncher(), NullLogger.Instance);

        var result = await saga.ResumeAsync("nonexistent", "nook-1", null);

        Assert.False(result.Success);
        Assert.Equal(ResumeOutcome.RunNotFound, result.Outcome);
    }

    [Fact]
    public async System.Threading.Tasks.Task ActiveRun_CannotBeResumed()
    {
        var svc = await NewSvcAsync();
        var (_, _, runId) = await SeedRunWithSegmentAsync(svc);

        var saga = new ResumeSaga(svc, new FakeProfileResolver(), new FakeNookHost(), new FakeResumeLauncher(), NullLogger.Instance);

        var result = await saga.ResumeAsync(runId, "nook-1", null);

        Assert.False(result.Success);
        Assert.Equal(ResumeOutcome.InvalidState, result.Outcome);
    }

    [Fact]
    public async System.Threading.Tasks.Task NoCapturedSession_SurfacesNudgeNotCrash()
    {
        var svc = await NewSvcAsync();
        var cardId = (await svc.CreateCardAsync("ws1", "no-session card", "user:test", "", 1, 2, null)).Id;
        var run = await svc.CreateRunAsync(cardId, "ws1", null);
        await svc.TransitionRunAsync(run!.Id, RunState.Interrupted);

        var saga = new ResumeSaga(svc, new FakeProfileResolver(), new FakeNookHost(), new FakeResumeLauncher(), NullLogger.Instance);

        var result = await saga.ResumeAsync(run.Id, "nook-1", null);

        Assert.False(result.Success);
        Assert.Equal(ResumeOutcome.NoCapturedSession, result.Outcome);
        Assert.Contains("session", result.Error!.ToLowerInvariant());

        var runAfter = svc.GetRun(run.Id);
        Assert.Equal("interrupted", runAfter!.State);
    }

    [Fact]
    public async System.Threading.Tasks.Task CompletedRun_CanBeResumed_TA56()
    {
        var svc = await NewSvcAsync();
        var (_, _, runId) = await SeedRunWithSegmentAsync(svc);
        await svc.TransitionRunAsync(runId, RunState.Completed);

        var resumeLauncher = new FakeResumeLauncher();
        var saga = new ResumeSaga(svc, new FakeProfileResolver(), new FakeNookHost(), resumeLauncher, NullLogger.Instance);

        var result = await saga.ResumeAsync(runId, "nook-new", null);

        Assert.True(result.Success, result.Error);
        Assert.Equal(ResumeOutcome.Resumed, result.Outcome);
        Assert.Equal("active", svc.GetRun(runId)!.State);
        Assert.Equal(1, resumeLauncher.ResumeCalls);
    }

    [Fact]
    public async System.Threading.Tasks.Task AdapterResumeFails_RevertsToInterrupted()
    {
        var svc = await NewSvcAsync();
        var (_, _, runId) = await SeedRunWithSegmentAsync(svc);
        await svc.TransitionRunAsync(runId, RunState.Interrupted);

        var resumeLauncher = new FakeResumeLauncher { ShouldFail = true, Error = "adapter crashed" };
        var saga = new ResumeSaga(svc, new FakeProfileResolver(), new FakeNookHost(), resumeLauncher, NullLogger.Instance);

        var result = await saga.ResumeAsync(runId, "nook-new", null);

        Assert.False(result.Success);
        Assert.Equal(ResumeOutcome.AdapterResumeFailed, result.Outcome);
        Assert.Contains("adapter crashed", result.Error);
        Assert.Equal("interrupted", svc.GetRun(runId)!.State);
    }

    [Fact]
    public async System.Threading.Tasks.Task EnvInjectionFails_ReturnsError()
    {
        var svc = await NewSvcAsync();
        var (_, _, runId) = await SeedRunWithSegmentAsync(svc);
        await svc.TransitionRunAsync(runId, RunState.Interrupted);

        var nookHost = new FakeNookHost { ShouldFailEnv = true };
        var saga = new ResumeSaga(svc, new FakeProfileResolver(), nookHost, new FakeResumeLauncher(), NullLogger.Instance);

        var result = await saga.ResumeAsync(runId, "nook-new", null);

        Assert.False(result.Success);
        Assert.Equal(ResumeOutcome.AdapterResumeFailed, result.Outcome);
    }

    [Fact]
    public async System.Threading.Tasks.Task AdapterOverride_OverridesResolvedAdapter()
    {
        var svc = await NewSvcAsync();
        var (_, _, runId) = await SeedRunWithSegmentAsync(svc);
        await svc.TransitionRunAsync(runId, RunState.Interrupted);

        var resumeLauncher = new FakeResumeLauncher();
        var saga = new ResumeSaga(svc, new FakeProfileResolver(), new FakeNookHost(), resumeLauncher, NullLogger.Instance);

        await saga.ResumeAsync(runId, "nook-new", "codex");

        Assert.Equal("codex", resumeLauncher.LastAdapter);
    }
}
