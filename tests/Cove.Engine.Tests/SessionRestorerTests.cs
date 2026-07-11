using Cove.Engine.Restart;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SessionRestorerTests
{
    private sealed record SpawnCall(RestorablePane Pane, string Command, string[] Args, string Cwd);

    private sealed class FakeSpawner : IRestoreSpawner
    {
        public List<SpawnCall> Calls { get; } = new();
        public void Respawn(RestorablePane pane, string command, string[] args, string cwd)
            => Calls.Add(new SpawnCall(pane, command, args, cwd));
    }

    private static RestorablePane Plain(string id, string cmd, string cwd)
        => new(id, cmd, new[] { "-l" }, cwd, null, null, null, null, false);

    private static RestorablePane Agent(string id, string adapter, string? sessionId, bool yolo)
        => new(id, "agent-launch", new[] { "--fresh" }, "/repo", "main", adapter, "claude", sessionId, yolo);

    private static ResumeCommand FakeResume(string adapter, string sessionId, LauncherOverrides o)
    {
        var args = new List<string> { "--resume", sessionId };
        if (o.Yolo)
            args.Add("--dangerously-skip-permissions");
        return new ResumeCommand(adapter, args, o.WorkingDir ?? "");
    }

    private static ResumeCommand ThrowResume(string adapter, string sessionId, LauncherOverrides o)
        => throw new ResumeFailedException("cannot build");

    [Fact]
    public void AgentWithSessionId_SpawnsResumeArgv_IntoSamePaneId_WithYolo()
    {
        var spawner = new FakeSpawner();
        var restorer = new SessionRestorer(spawner, FakeResume, NullLogger.Instance);

        var summary = restorer.Restore(new RestorablePane?[] { Agent("pane-1", "claude-code", "sess-xyz", yolo: true) }, enabled: true);

        Assert.Equal(1, summary.Restored);
        Assert.Equal(0, summary.Fresh);
        Assert.Equal(0, summary.Skipped);
        var call = Assert.Single(spawner.Calls);
        Assert.Equal("pane-1", call.Pane.PaneId);
        Assert.Contains("--resume", call.Args);
        Assert.Contains("sess-xyz", call.Args);
        Assert.Contains("--dangerously-skip-permissions", call.Args);
    }

    [Fact]
    public void AgentWithSessionId_ResumeBuildFails_FallsBackToFreshLaunchArgv()
    {
        var spawner = new FakeSpawner();
        var restorer = new SessionRestorer(spawner, ThrowResume, NullLogger.Instance);

        var summary = restorer.Restore(new RestorablePane?[] { Agent("pane-2", "claude-code", "sess-xyz", yolo: false) }, enabled: true);

        Assert.Equal(0, summary.Restored);
        Assert.Equal(1, summary.Fresh);
        var call = Assert.Single(spawner.Calls);
        Assert.Equal("agent-launch", call.Command);
        Assert.Equal(new[] { "--fresh" }, call.Args);
        Assert.DoesNotContain("--resume", call.Args);
    }

    [Fact]
    public void AgentWithoutSessionId_FreshLaunches()
    {
        var spawner = new FakeSpawner();
        var restorer = new SessionRestorer(spawner, FakeResume, NullLogger.Instance);

        var summary = restorer.Restore(new RestorablePane?[] { Agent("pane-3", "claude-code", null, yolo: false) }, enabled: true);

        Assert.Equal(1, summary.Fresh);
        var call = Assert.Single(spawner.Calls);
        Assert.Equal("agent-launch", call.Command);
    }

    [Fact]
    public void PlainPane_RespawnsShellInCwd()
    {
        var spawner = new FakeSpawner();
        var restorer = new SessionRestorer(spawner, FakeResume, NullLogger.Instance);

        var summary = restorer.Restore(new RestorablePane?[] { Plain("pane-4", "/bin/zsh", "/home/moh/proj") }, enabled: true);

        Assert.Equal(1, summary.Restored);
        Assert.Equal(0, summary.Fresh);
        var call = Assert.Single(spawner.Calls);
        Assert.Equal("/bin/zsh", call.Command);
        Assert.Equal("/home/moh/proj", call.Cwd);
    }

    [Fact]
    public void NoUsableRecord_SkippedWithoutSpawning()
    {
        var spawner = new FakeSpawner();
        var restorer = new SessionRestorer(spawner, FakeResume, NullLogger.Instance);

        var summary = restorer.Restore(new RestorablePane?[] { null }, enabled: true);

        Assert.Equal(0, summary.Restored);
        Assert.Equal(0, summary.Fresh);
        Assert.Equal(1, summary.Skipped);
        Assert.Empty(spawner.Calls);
    }

    [Fact]
    public void ConfigFlagOff_NothingSpawns()
    {
        var spawner = new FakeSpawner();
        var restorer = new SessionRestorer(spawner, FakeResume, NullLogger.Instance);

        var summary = restorer.Restore(new RestorablePane?[]
        {
            Agent("pane-5", "claude-code", "sess-xyz", yolo: true),
            Plain("pane-6", "/bin/zsh", "/tmp"),
        }, enabled: false);

        Assert.Equal(0, summary.Restored);
        Assert.Equal(0, summary.Fresh);
        Assert.Equal(0, summary.Skipped);
        Assert.Empty(spawner.Calls);
    }
}
