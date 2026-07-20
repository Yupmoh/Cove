using System.Linq;
using System.Text.Json;
using Cove.Engine;
using Cove.Engine.Protocol;
using Cove.Protocol;
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
        var orch = LaunchTestFactory.Create();
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
        var orch = LaunchTestFactory.Create();
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
        var orch = LaunchTestFactory.Create();
        var profile = NewProfile();
        var overrides = new LauncherOverrides { ExtraFlags = new[] { "--verbose", "--model=opus" } };

        var cmd = orch.BuildLaunchCommand(profile, overrides);

        Assert.Contains("--verbose", cmd.Args);
        Assert.Contains("--model=opus", cmd.Args);
    }

    [Fact]
    public void BuildLaunchCommand_EnvVarsApplied()
    {
        var orch = LaunchTestFactory.Create();
        var profile = NewProfile();
        var overrides = new LauncherOverrides { Env = new Dictionary<string, string> { ["FOO"] = "bar" } };

        var cmd = orch.BuildLaunchCommand(profile, overrides);

        Assert.Contains("--env=FOO=bar", cmd.Args);
    }

    [Theory]
    [InlineData("claude-code", "--effort", "override-effort")]
    [InlineData("codex", "--config", "model_reasoning_effort=\"override-effort\"")]
    [InlineData("omp", "--thinking", "override-effort")]
    [InlineData("pi", "--thinking", "override-effort")]
    public void BuildLaunchCommand_ExplicitSelectionsOverrideProfile(
        string adapter,
        string effortFlag,
        string effortValue)
    {
        var profile = NewProfile(adapter) with
        {
            Model = "profile-model",
            Effort = "profile-effort",
            CliArgs = ["agent"],
        };
        var overrides = new LauncherOverrides
        {
            Model = "override-model",
            Effort = "override-effort",
        };

        var command = LaunchTestFactory.Create()
            .BuildLaunchCommand(profile, overrides);

        Assert.Equal(
            ["--model", "override-model", effortFlag, effortValue],
            command.Args);
    }

    [Theory]
    [InlineData("claude-code", "--effort", "profile-effort")]
    [InlineData("codex", "--config", "model_reasoning_effort=\"profile-effort\"")]
    [InlineData("omp", "--thinking", "profile-effort")]
    [InlineData("pi", "--thinking", "profile-effort")]
    public void BuildLaunchCommand_OmittedSelectionsUseProfile(
        string adapter,
        string effortFlag,
        string effortValue)
    {
        var profile = NewProfile(adapter) with
        {
            Model = "profile-model",
            Effort = "profile-effort",
            CliArgs = ["agent"],
        };

        var command = LaunchTestFactory.Create()
            .BuildLaunchCommand(profile, new LauncherOverrides());

        Assert.Equal(
            ["--model", "profile-model", effortFlag, effortValue],
            command.Args);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", " ")]
    [InlineData("default", "DEFAULT")]
    public void BuildLaunchCommand_EmptyOrDefaultSelectionsAreSuppressed(
        string? model,
        string? effort)
    {
        var profile = NewProfile("pi") with
        {
            Model = model,
            Effort = effort,
            CliArgs = ["agent"],
        };

        var command = LaunchTestFactory.Create()
            .BuildLaunchCommand(profile, new LauncherOverrides());

        Assert.Empty(command.Args);
    }

    [Fact]
    public void BuildFlagsJson_UsesExplicitSelectionsOverProfile()
    {
        var profile = NewProfile("claude-code") with
        {
            Model = "profile-model",
            Effort = "profile-effort",
        };
        var composer = new LaunchCommandComposer();

        var json = composer.BuildFlagsJson(
            profile,
            new LauncherOverrides
            {
                Model = "override-model",
                Effort = "override-effort",
            });
        using var document = System.Text.Json.JsonDocument.Parse(json);

        Assert.Equal(
            "override-model",
            document.RootElement.GetProperty("model").GetString());
        Assert.Equal(
            "override-effort",
            document.RootElement.GetProperty("effort").GetString());
    }

    [Fact]
    public async Task OverrideCommands_SaveAndReturnSelections()
    {
        var launcher = LaunchTestFactory.Create();
        var save = await EngineCommandRouter.RouteAsync(
            new ControlRequest(
                "save",
                "cove://commands/launch.overrides.save",
                JsonSerializer.SerializeToElement(
                    new LaunchOverrideSaveParams(
                        "nook-1",
                        false,
                        null,
                        [],
                        new Dictionary<string, string>(),
                        "model-x",
                        "high"),
                    CoveJsonContext.Default.LaunchOverrideSaveParams)),
            launcher: launcher);
        var get = await EngineCommandRouter.RouteAsync(
            new ControlRequest(
                "get",
                "cove://commands/launch.overrides.get",
                JsonSerializer.SerializeToElement(
                    new LaunchOverrideGetParams("nook-1"),
                    CoveJsonContext.Default.LaunchOverrideGetParams)),
            launcher: launcher);

        Assert.True(save!.Ok, save.Error?.Message);
        Assert.True(get!.Ok, get.Error?.Message);
        var overrides = get.Data!.Value.Deserialize(
            CoveJsonContext.Default.LauncherOverridesDto)!;
        Assert.Equal("model-x", overrides.Model);
        Assert.Equal("high", overrides.Effort);
    }

    [Fact]
    public async Task ResumeAsync_WithoutAdapterService_FallsBackToLaunchCommand()
    {
        var orch = LaunchTestFactory.Create();
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
        var orch = LaunchTestFactory.Create(resume: resumeService);
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
        var orch = LaunchTestFactory.Create();
        var overrides = new LauncherOverrides { Yolo = true, ExtraFlags = new[] { "--verbose" } };

        orch.PersistOverrides("p1", overrides);
        var retrieved = orch.GetOverrides("p1");

        Assert.NotNull(retrieved);
        Assert.True(retrieved!.Yolo);
        Assert.Contains("--verbose", retrieved.ExtraFlags);
    }

    [Fact]
    public void PersistOverrides_UnknownNook_ReturnsNull()
    {
        var orch = LaunchTestFactory.Create();
        Assert.Null(orch.GetOverrides("nonexistent"));
    }

    [Fact]
    public void ClearOverrides_RemovesEntry()
    {
        var orch = LaunchTestFactory.Create();
        orch.PersistOverrides("p1", new LauncherOverrides { Yolo = true });
        orch.ClearOverrides("p1");
        Assert.Null(orch.GetOverrides("p1"));
    }

    private sealed class StubResumeAdapter : IAdapterResume
    {
        public bool BuildResumeCalled;
        public Task<ResumeCommand> BuildResumeCommandAsync(
            string adapter,
            string sessionId,
            LauncherOverrides overrides,
            CancellationToken cancellationToken)
        {
            BuildResumeCalled = true;
            return Task.FromResult(
                new ResumeCommand("agent", new[] { "--resume", sessionId }, ""));
        }

        public Task WaitForReadiness(string sessionId, System.Threading.CancellationToken cancellationToken) => Task.CompletedTask;

        public bool IsSessionReaped(string sessionId) => false;
    }
}
