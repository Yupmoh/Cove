using Cove.Engine.Restart;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SessionRestorerTests
{
    private sealed class CapturingLogger : ILogger
    {
        public List<string> Messages { get; } = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => new Noop();
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, System.Exception? exception, System.Func<TState, System.Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
        private sealed class Noop : IDisposable { public void Dispose() { } }
    }

    private sealed record SpawnCall(RestorableNook Nook, string Command, string[] Args, string Cwd);

    private sealed class FakeSpawner : IRestoreSpawner
    {
        public List<SpawnCall> Calls { get; } = new();
        public void Respawn(RestorableNook nook, string command, string[] args, string cwd)
            => Calls.Add(new SpawnCall(nook, command, args, cwd));
    }

    private static RestorableNook Plain(string id, string cmd, string cwd)
        => new(id, cmd, new[] { "-l" }, cwd, null, null, null, null, false);

    private static RestorableNook Agent(string id, string adapter, string? sessionId, bool yolo)
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
    public void AgentWithSessionId_SpawnsResumeArgv_IntoSameNookId_WithYolo()
    {
        var spawner = new FakeSpawner();
        var restorer = new SessionRestorer(spawner, FakeResume, NullLogger.Instance);

        var summary = restorer.Restore(new RestorableNook?[] { Agent("nook-1", "claude-code", "sess-xyz", yolo: true) }, enabled: true);

        Assert.Equal(1, summary.Restored);
        Assert.Equal(0, summary.Fresh);
        Assert.Equal(0, summary.Skipped);
        var call = Assert.Single(spawner.Calls);
        Assert.Equal("nook-1", call.Nook.NookId);
        Assert.Contains("--resume", call.Args);
        Assert.Contains("sess-xyz", call.Args);
        Assert.Contains("--dangerously-skip-permissions", call.Args);
    }

    [Fact]
    public void AgentWithSessionId_ResumeBuildFails_FallsBackToFreshLaunchArgv()
    {
        var spawner = new FakeSpawner();
        var restorer = new SessionRestorer(spawner, ThrowResume, NullLogger.Instance);

        var summary = restorer.Restore(new RestorableNook?[] { Agent("nook-2", "claude-code", "sess-xyz", yolo: false) }, enabled: true);

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

        var summary = restorer.Restore(new RestorableNook?[] { Agent("nook-3", "claude-code", null, yolo: false) }, enabled: true);

        Assert.Equal(1, summary.Fresh);
        var call = Assert.Single(spawner.Calls);
        Assert.Equal("agent-launch", call.Command);
    }

    [Fact]
    public void AgentWithoutSessionId_LogsEmptySessionIdReason()
    {
        var spawner = new FakeSpawner();
        var logger = new CapturingLogger();
        var restorer = new SessionRestorer(spawner, FakeResume, logger);

        restorer.Restore(new RestorableNook?[] { Agent("nook-empty", "claude-code", null, yolo: false) }, enabled: true);

        Assert.Contains(logger.Messages, m => m.Contains("no persisted sessionId"));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("resume command build failed"));
    }

    [Fact]
    public void AgentWithSessionId_ButBuildFails_LogsBuildFailureReason()
    {
        var spawner = new FakeSpawner();
        var logger = new CapturingLogger();
        var restorer = new SessionRestorer(spawner, ThrowResume, logger);

        restorer.Restore(new RestorableNook?[] { Agent("nook-build", "claude-code", "sess-xyz", yolo: false) }, enabled: true);

        Assert.Contains(logger.Messages, m => m.Contains("resume command build failed"));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("no persisted sessionId"));
    }

    [Fact]
    public void PlainNook_RespawnsShellInCwd()
    {
        var spawner = new FakeSpawner();
        var restorer = new SessionRestorer(spawner, FakeResume, NullLogger.Instance);

        var summary = restorer.Restore(new RestorableNook?[] { Plain("nook-4", "/bin/zsh", "/home/moh/proj") }, enabled: true);

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

        var summary = restorer.Restore(new RestorableNook?[] { null }, enabled: true);

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

        var summary = restorer.Restore(new RestorableNook?[]
        {
            Agent("nook-5", "claude-code", "sess-xyz", yolo: true),
            Plain("nook-6", "/bin/zsh", "/tmp"),
        }, enabled: false);

        Assert.Equal(0, summary.Restored);
        Assert.Equal(0, summary.Fresh);
        Assert.Equal(0, summary.Skipped);
        Assert.Empty(spawner.Calls);
    }
}
