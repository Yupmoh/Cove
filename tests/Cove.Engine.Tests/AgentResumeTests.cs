using Cove.Engine.Restart;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class AgentResumeTests
{
    private sealed class FakeAdapter : IAdapterResume
    {
        public string ResumeArg { get; set; } = "--resume";
        public bool Ready { get; set; } = true;
        public bool Reaped { get; set; }
        public bool FailBuild { get; set; }
        public int ReadinessCalls { get; private set; }

        public Task<ResumeCommand> BuildResumeCommandAsync(
            string adapter,
            string sessionId,
            LauncherOverrides overrides,
            CancellationToken cancellationToken)
        {
            if (FailBuild)
                throw new ResumeFailedException("adapter build failed");
            var args = new List<string> { ResumeArg, sessionId };
            if (overrides.Yolo)
                args.Add("--dangerously-skip-permissions");
            foreach (var env in overrides.Env)
                args.Add($"--env={env.Key}={env.Value}");
            return Task.FromResult(
                new ResumeCommand("agent", args, overrides.WorkingDir ?? ""));
        }

        public async Task WaitForReadiness(string sessionId, CancellationToken cancellationToken)
        {
            ReadinessCalls++;
            if (!Ready)
                throw new ResumeFailedException("agent not ready");
            await Task.Delay(10, cancellationToken);
        }

        public bool IsSessionReaped(string sessionId) => Reaped;
    }

    private static LauncherOverrides Overrides(bool yolo = false, string? cwd = null) => new()
    {
        Yolo = yolo,
        WorkingDir = cwd,
        Env = new Dictionary<string, string>(),
    };

    [Fact]
    public async Task Resume_Succeeds_WhenAdapterReadyAndSessionAlive()
    {
        var adapter = new FakeAdapter { Ready = true, Reaped = false };
        var svc = new AgentResumeService(adapter);
        var state = await svc.ResumeAsync("claude-code", "sess-1", Overrides(yolo: true, "/repo"), cancellationToken: default);

        Assert.Equal(AgentResumeState.Succeeded, state.State);
        Assert.NotNull(state.Command);
        Assert.Contains("--resume", state.Command!.Args);
        Assert.Contains("--dangerously-skip-permissions", state.Command.Args);
        Assert.Equal("/repo", state.Command.Cwd);
        Assert.Equal(1, adapter.ReadinessCalls);
    }

    [Fact]
    public async Task Resume_Fails_WhenAdapterNotReady_SurfacesNudge_NeverSilentFresh()
    {
        var adapter = new FakeAdapter { Ready = false };
        var svc = new AgentResumeService(adapter);
        var state = await svc.ResumeAsync("claude-code", "sess-1", Overrides(), cancellationToken: default);

        Assert.Equal(AgentResumeState.Failed, state.State);
        Assert.NotNull(state.Nudge);
        Assert.Equal(NudgeKind.RetryOrStartFresh, state.Nudge!.Kind);
        Assert.Null(state.Command);
    }

    [Fact]
    public async Task Resume_ReapedSession_FreshLaunches_AndReappliesOverrides()
    {
        var adapter = new FakeAdapter { Reaped = true, Ready = true };
        var svc = new AgentResumeService(adapter);
        var overrides = Overrides(yolo: true, "/repo");
        var state = await svc.ResumeAsync("claude-code", "sess-1", overrides, cancellationToken: default);

        Assert.Equal(AgentResumeState.Succeeded, state.State);
        Assert.NotNull(state.Command);
        Assert.Contains("--dangerously-skip-permissions", state.Command!.Args);
        Assert.DoesNotContain("--resume", state.Command.Args);
    }

    [Fact]
    public async Task Resume_BuildThrows_SurfacesNudge_NotCrash()
    {
        var adapter = new FakeAdapter { FailBuild = true };
        var svc = new AgentResumeService(adapter);
        var state = await svc.ResumeAsync("claude-code", "sess-1", Overrides(), cancellationToken: default);

        Assert.Equal(AgentResumeState.Failed, state.State);
        Assert.NotNull(state.Nudge);
    }

    [Fact]
    public async Task Resume_Cancellation_DoesNotProduceSuccess()
    {
        var adapter = new FakeAdapter { Ready = true };
        var svc = new AgentResumeService(adapter);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var state = await svc.ResumeAsync("claude-code", "sess-1", Overrides(), cts.Token);

        Assert.NotEqual(AgentResumeState.Succeeded, state.State);
    }
}
