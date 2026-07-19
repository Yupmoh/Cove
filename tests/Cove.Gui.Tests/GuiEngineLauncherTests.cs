using Cove.Platform;
using Cove.Protocol;
using Xunit;

namespace Cove.Gui.Tests;

public sealed class GuiEngineLauncherTests
{
    [Fact]
    public async Task ExistingEndpoint_ReturnsConnectionWithoutLaunching()
    {
        var launcher = new FakeLauncher();
        var expected = new MemoryStream();

        var actual = await GuiEngineLauncher.ConnectOrSpawnAsync(
            "dev",
            launcher,
            (_, _) => Task.FromResult<Stream>(expected),
            static (_, _) => Task.CompletedTask,
            static () => 0,
            CancellationToken.None);

        Assert.Same(expected, actual);
        Assert.Empty(launcher.Channels);
    }

    [Fact]
    public async Task FailedInitialDial_LaunchesOnceAndRetries()
    {
        var launcher = new FakeLauncher();
        var expected = new MemoryStream();
        var dialCount = 0;

        var actual = await GuiEngineLauncher.ConnectOrSpawnAsync(
            "beta",
            launcher,
            (_, _) => ++dialCount == 1
                ? Task.FromException<Stream>(new IOException("offline"))
                : Task.FromResult<Stream>(expected),
            static (_, _) => Task.CompletedTask,
            static () => 0,
            CancellationToken.None);

        Assert.Same(expected, actual);
        Assert.Equal(2, dialCount);
        Assert.Equal(["beta"], launcher.Channels);
    }

    [Fact]
    public async Task ReadinessTimeout_RemainsObservable()
    {
        var launcher = new FakeLauncher();
        long elapsed = 0;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => GuiEngineLauncher.ConnectOrSpawnAsync(
                "dev",
                launcher,
                static (_, _) => Task.FromException<Stream>(
                    new IOException("offline")),
                (_, _) =>
                {
                    elapsed += ProtocolConstants.ReadinessTimeoutMs;
                    return Task.CompletedTask;
                },
                () => elapsed,
                CancellationToken.None));

        Assert.Contains("did not become connectable", error.Message);
        Assert.Equal(["dev"], launcher.Channels);
    }

    [Fact]
    public async Task RetryCancellation_RemainsObservable()
    {
        var launcher = new FakeLauncher();
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => GuiEngineLauncher.ConnectOrSpawnAsync(
                "stable",
                launcher,
                static (_, _) => Task.FromException<Stream>(
                    new IOException("offline")),
                (_, token) =>
                {
                    cancellation.Cancel();
                    return Task.Delay(Timeout.InfiniteTimeSpan, token);
                },
                static () => 0,
                cancellation.Token));

        Assert.Equal(["stable"], launcher.Channels);
    }

    private sealed class FakeLauncher : IEngineProcessLauncher
    {
        public List<string> Channels { get; } = [];

        public void Launch(string channel) => Channels.Add(channel);
    }
}
