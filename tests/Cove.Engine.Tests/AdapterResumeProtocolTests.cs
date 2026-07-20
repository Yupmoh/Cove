using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cove.Adapters;
using Cove.Engine.Launch;
using Cove.Engine.Restart;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class AdapterResumeProtocolTests
{
    private static string WriteFixture(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-resume-" + Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(root, name);
        Directory.CreateDirectory(dir);
        var src = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..",
            "tests", "fixtures", "adapters", name);
        foreach (var f in Directory.GetFiles(src))
            File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true);
        return root;
    }

    [Fact]
    public async Task BuildResumeCommandAsync_ManifestProtocol_ReturnsCommand()
    {
        var root = WriteFixture("test-v2");
        var store = new AdapterManifestStore(root);
        var runner = new MethodRunner();
        var proto = new AdapterResumeProtocol(store, runner);
        var overrides = new LauncherOverrides { WorkingDir = "/tmp/work" };

        var cmd = await proto.BuildResumeCommandAsync("test-v2", "sess-123", overrides);

        Assert.NotNull(cmd);
        Assert.Equal("test-v2", cmd!.Command);
        Assert.Contains("resume", cmd.Args);
        Assert.Contains("sess-123", cmd.Args);
    }

    [Fact]
    public async Task BuildResumeCommandAsync_UnknownAdapter_Throws()
    {
        var root = WriteFixture("test-v2");
        var store = new AdapterManifestStore(root);
        var runner = new MethodRunner();
        var proto = new AdapterResumeProtocol(store, runner);

        await Assert.ThrowsAsync<ResumeFailedException>(() =>
            proto.BuildResumeCommandAsync("never-installed", "sess-1", new LauncherOverrides()));
    }

    [Fact]
    public async Task BuildResumeCommandAsync_AdapterWithoutResumeMethod_Throws()
    {
        var root = WriteFixture("test-v1");
        var store = new AdapterManifestStore(root);
        var runner = new MethodRunner();
        var proto = new AdapterResumeProtocol(store, runner);

        await Assert.ThrowsAsync<ResumeFailedException>(() =>
            proto.BuildResumeCommandAsync("test-v1", "sess-1", new LauncherOverrides()));
    }

    [Fact]
    public void BuildResumeCommand_Sync_DelegatesToAsync()
    {
        var root = WriteFixture("test-v2");
        var store = new AdapterManifestStore(root);
        var runner = new MethodRunner();
        var proto = new AdapterResumeProtocol(store, runner);

        var cmd = proto.BuildResumeCommand("sess-1", new LauncherOverrides { WorkingDir = "/tmp" });

        Assert.NotNull(cmd);
        Assert.Equal("test-v2", cmd.Command);
    }

    [Fact]
    public async Task BuildResumeCommandAsync_IncludesSessionIdInFlags()
    {
        var root = WriteFixture("test-v2");
        var store = new AdapterManifestStore(root);
        var runner = new MethodRunner();
        var proto = new AdapterResumeProtocol(store, runner);
        var overrides = new LauncherOverrides { Yolo = true };

        var cmd = await proto.BuildResumeCommandAsync("test-v2", "abc-999", overrides);

        Assert.NotNull(cmd);
        Assert.Contains("abc-999", cmd!.Args);
    }

    [Fact]
    public async Task WaitForReadiness_NoOp_ReturnsCompleted()
    {
        var root = WriteFixture("test-v2");
        var store = new AdapterManifestStore(root);
        var runner = new MethodRunner();
        var proto = new AdapterResumeProtocol(store, runner);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await proto.WaitForReadiness("sess-1", cts.Token);
    }

    [Fact]
    public void IsSessionReaped_DefaultFalse()
    {
        var root = WriteFixture("test-v2");
        var store = new AdapterManifestStore(root);
        var runner = new MethodRunner();
        var proto = new AdapterResumeProtocol(store, runner);

        Assert.False(proto.IsSessionReaped("any-session"));
    }

    [Fact]
    public void WindowsResumeCommand_OmpUsesNativeBinaryAndSessionId()
    {
        var command = WindowsAdapterResumeCommand.Build(
            "omp",
            @"C:\tools\omp.exe",
            @"C:\cove\adapters\omp",
            "omp-session",
            new LauncherOverrides { WorkingDir = @"D:\Cove" });

        Assert.NotNull(command);
        Assert.Equal(@"C:\tools\omp.exe", command.Command);
        Assert.Equal("--resume", command.Args[0]);
        Assert.Equal("omp-session", command.Args[1]);
        Assert.Contains(Path.Combine(@"C:\cove\adapters\omp", "cove-hooks.ts"), command.Args);
    }

    [Fact]
    public void WindowsResumeCommand_ClaudePreservesPermissionOverride()
    {
        var command = WindowsAdapterResumeCommand.Build(
            "claude-code",
            @"C:\tools\claude.exe",
            @"C:\cove\adapters\claude-code",
            "claude-session",
            new LauncherOverrides { Yolo = true });

        Assert.NotNull(command);
        Assert.Equal(["--resume", "claude-session", "--dangerously-skip-permissions"], command.Args);
    }

    [Fact]
    public void WindowsLaunchCommand_ClaudeUsesNativeBinaryAndProfileOptions()
    {
        var profile = new LaunchProfile(
            "Default",
            "default",
            "claude-code",
            true,
            "sonnet",
            null,
            [],
            new Dictionary<string, string>(),
            new Dictionary<string, bool>(),
            [],
            null,
            1);

        var command = WindowsAdapterLaunchCommand.Build(
            @"C:\tools\claude.exe",
            @"C:\cove\adapters\claude-code",
            profile,
            new LauncherOverrides { Yolo = true, WorkingDir = @"D:\Cove" });

        Assert.NotNull(command);
        Assert.Equal(@"C:\tools\claude.exe", command.Command);
        Assert.Equal(["--dangerously-skip-permissions", "--model", "sonnet"], command.Args);
        Assert.Equal(@"D:\Cove", command.Cwd);
    }

    [Fact]
    public void WindowsLaunchCommand_CodexWrapsNpmCommandShim()
    {
        var profile = new LaunchProfile(
            "Default",
            "default",
            "codex",
            true,
            null,
            null,
            [],
            new Dictionary<string, string>(),
            new Dictionary<string, bool>(),
            [],
            null,
            1);

        var command = WindowsAdapterLaunchCommand.Build(
            @"C:\Users\test\npm\codex.cmd",
            @"C:\cove\adapters\codex",
            profile,
            new LauncherOverrides { Yolo = true, WorkingDir = @"D:\Cove" });

        Assert.NotNull(command);
        Assert.Equal("cmd.exe", command.Command);
        Assert.Equal(
            ["/d", "/s", "/c", @"C:\Users\test\npm\codex.cmd", "--dangerously-bypass-hook-trust", "--yolo"],
            command.Args);
        Assert.Equal(@"D:\Cove", command.Cwd);
    }
}
