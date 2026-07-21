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
        var probeConnection = new MemoryStream();
        var expected = new MemoryStream();
        var dialCount = 0;

        var actual = await GuiEngineLauncher.ConnectOrSpawnAsync(
            "dev",
            "0.5.2",
            launcher,
            (_, _) => Task.FromResult<Stream>(
                ++dialCount == 1 ? probeConnection : expected),
            static (_, expectedVersion, _) =>
                Task.FromResult(new EngineProbeResult(true, expectedVersion)),
            static (_, _) => Task.CompletedTask,
            static () => 0,
            CancellationToken.None);

        Assert.Same(expected, actual);
        Assert.False(probeConnection.CanRead);
        Assert.Equal(2, dialCount);
        Assert.Empty(launcher.Channels);
    }

    [Fact]
    public async Task FailedInitialDial_LaunchesOnceAndRetries()
    {
        var launcher = new FakeLauncher();
        var probeConnection = new MemoryStream();
        var expected = new MemoryStream();
        var dialCount = 0;

        var actual = await GuiEngineLauncher.ConnectOrSpawnAsync(
            "beta",
            "0.5.2",
            launcher,
            (_, _) => ++dialCount switch
            {
                1 => Task.FromException<Stream>(new IOException("offline")),
                2 => Task.FromResult<Stream>(probeConnection),
                _ => Task.FromResult<Stream>(expected),
            },
            static (_, expectedVersion, _) =>
                Task.FromResult(new EngineProbeResult(true, expectedVersion)),
            static (_, _) => Task.CompletedTask,
            static () => 0,
            CancellationToken.None);

        Assert.Same(expected, actual);
        Assert.False(probeConnection.CanRead);
        Assert.Equal(3, dialCount);
        Assert.Equal(["beta"], launcher.Channels);
    }

    [Fact]
    public async Task IncompatibleEndpoint_LaunchesHandoffSuccessorOnce()
    {
        var launcher = new FakeLauncher();
        var stale = new MemoryStream();
        var staleDuringHandoff = new MemoryStream();
        var matchingProbe = new MemoryStream();
        var expected = new MemoryStream();
        var dialCount = 0;
        var probeCount = 0;
        long elapsed = 0;

        var actual = await GuiEngineLauncher.ConnectOrSpawnAsync(
            "stable",
            "0.5.2",
            launcher,
            (_, _) => ++dialCount switch
            {
                1 => Task.FromResult<Stream>(stale),
                2 => Task.FromResult<Stream>(staleDuringHandoff),
                3 => Task.FromResult<Stream>(matchingProbe),
                _ => Task.FromResult<Stream>(expected),
            },
            (_, _, _) => Task.FromResult(
                ++probeCount < 3
                    ? new EngineProbeResult(false, "0.5.1")
                    : new EngineProbeResult(true, "0.5.2")),
            (_, _) =>
            {
                elapsed += ProtocolConstants.SpawnPollMs;
                return Task.CompletedTask;
            },
            () => elapsed,
            CancellationToken.None);

        Assert.Same(expected, actual);
        Assert.False(stale.CanRead);
        Assert.False(staleDuringHandoff.CanRead);
        Assert.False(matchingProbe.CanRead);
        Assert.Equal(4, dialCount);
        Assert.Equal(3, probeCount);
        Assert.Equal(["stable"], launcher.Channels);
    }

    [Fact]
    public async Task ReadinessTimeout_RemainsObservable()
    {
        var launcher = new FakeLauncher();
        long elapsed = 0;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => GuiEngineLauncher.ConnectOrSpawnAsync(
                "dev",
                "0.5.2",
                launcher,
                static (_, _) => Task.FromException<Stream>(
                    new IOException("offline")),
                static (_, _, _) => throw new InvalidOperationException(
                    "offline streams must not be probed"),
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
                "0.5.2",
                launcher,
                static (_, _) => Task.FromException<Stream>(
                    new IOException("offline")),
                static (_, _, _) => throw new InvalidOperationException(
                    "offline streams must not be probed"),
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
