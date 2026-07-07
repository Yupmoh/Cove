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
}
