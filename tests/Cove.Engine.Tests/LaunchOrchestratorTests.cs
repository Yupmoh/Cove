using System.Linq;
using Cove.Adapters;
using Cove.Engine.Launch;
using Cove.Engine.Restart;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class LaunchOrchestratorTests
{
    private static LaunchProfile NewProfile(string adapter = "claude-code") => new(
        Name: "test",
        Slug: "test",
        Adapter: adapter,
        IsDefault: false,
        Model: null,
        Effort: null,
        CliArgs: new[] { "claude", "--no-update" },
        Env: new Dictionary<string, string>(),
        Permissions: new Dictionary<string, bool>(),
        Skills: new List<string>(),
        Agent: null,
        SchemaVersion: 1);

    [Fact]
    public void BuildLaunchCommand_AppliesYoloOverride()
    {
        var orch = new LaunchOrchestrator();
        var profile = NewProfile();
        var overrides = new LauncherOverrides { Yolo = true, WorkingDir = "/tmp" };

        var cmd = orch.BuildLaunchCommand(profile, overrides);

        Assert.Equal("claude", cmd.Command);
        Assert.Contains("--dangerously-skip-permissions", cmd.Args);
        Assert.Equal("/tmp", cmd.Cwd);
    }

    [Fact]
    public void BuildLaunchCommand_BinaryNotDuplicatedInArgs()
    {
        var orch = new LaunchOrchestrator();
        var profile = NewProfile();
        var overrides = new LauncherOverrides();

        var cmd = orch.BuildLaunchCommand(profile, overrides);

        Assert.Equal("claude", cmd.Command);
        Assert.DoesNotContain("claude", cmd.Args);
        Assert.Contains("--no-update", cmd.Args);
    }

    [Fact]
    public void BuildLaunchCommand_CustomFlagsApplied()
    {
        var orch = new LaunchOrchestrator();
        var profile = NewProfile();
        var overrides = new LauncherOverrides { ExtraFlags = new[] { "--verbose", "--model=opus" } };

        var cmd = orch.BuildLaunchCommand(profile, overrides);

        Assert.Contains("--verbose", cmd.Args);
        Assert.Contains("--model=opus", cmd.Args);
    }

    [Fact]
    public void BuildLaunchCommand_EnvVarsApplied()
    {
        var orch = new LaunchOrchestrator();
        var profile = NewProfile();
        var overrides = new LauncherOverrides { Env = new Dictionary<string, string> { ["FOO"] = "bar" } };

        var cmd = orch.BuildLaunchCommand(profile, overrides);

        Assert.Contains("--env=FOO=bar", cmd.Args);
    }

    [Fact]
    public async Task ResumeAsync_WithoutAdapterService_FallsBackToLaunchCommand()
    {
        var orch = new LaunchOrchestrator();
        var profile = NewProfile();
        var overrides = new LauncherOverrides { Yolo = true };

        var result = await orch.ResumeAsync(profile, "session-123", overrides);

        Assert.Equal(AgentResumeState.Succeeded, result.State);
        Assert.NotNull(result.Command);
        Assert.Contains("--dangerously-skip-permissions", result.Command!.Args);
    }

    [Fact]
    public async Task ResumeAsync_WithAdapterService_DelegatesToSeam()
    {
        var stubAdapter = new StubResumeAdapter();
        var resumeService = new AgentResumeService(stubAdapter);
        var orch = new LaunchOrchestrator(resumeService);
        var profile = NewProfile();
        var overrides = new LauncherOverrides { Yolo = true };

        var result = await orch.ResumeAsync(profile, "session-123", overrides);

        Assert.Equal(AgentResumeState.Succeeded, result.State);
        Assert.Equal("session-123", result.SessionId);
        Assert.True(stubAdapter.BuildResumeCalled);
    }

    [Fact]
    public void PersistOverrides_StoreAndRetrieve()
    {
        var orch = new LaunchOrchestrator();
        var overrides = new LauncherOverrides { Yolo = true, ExtraFlags = new[] { "--verbose" } };

        orch.PersistOverrides("p1", overrides);
        var retrieved = orch.GetOverrides("p1");

        Assert.NotNull(retrieved);
        Assert.True(retrieved!.Yolo);
        Assert.Contains("--verbose", retrieved.ExtraFlags);
    }

    [Fact]
    public void PersistOverrides_UnknownPane_ReturnsNull()
    {
        var orch = new LaunchOrchestrator();
        Assert.Null(orch.GetOverrides("nonexistent"));
    }

    [Fact]
    public void ClearOverrides_RemovesEntry()
    {
        var orch = new LaunchOrchestrator();
        orch.PersistOverrides("p1", new LauncherOverrides { Yolo = true });
        orch.ClearOverrides("p1");
        Assert.Null(orch.GetOverrides("p1"));
    }

    private sealed class StubResumeAdapter : IAdapterResume
    {
        public bool BuildResumeCalled;
        public ResumeCommand BuildResumeCommand(string sessionId, LauncherOverrides overrides)
        {
            BuildResumeCalled = true;
            return new ResumeCommand("agent", new[] { "--resume", sessionId }, "");
        }

        public Task WaitForReadiness(string sessionId, System.Threading.CancellationToken cancellationToken) => Task.CompletedTask;

        public bool IsSessionReaped(string sessionId) => false;
    }
}
