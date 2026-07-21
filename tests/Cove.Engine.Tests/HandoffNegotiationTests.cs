using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cove.Engine.Agents;
using Cove.Engine.Browser;
using Cove.Engine.Daemon;
using Cove.Engine.Hooks;
using Cove.Engine.Pty;
using Cove.Engine.Restart;
using Cove.Engine.Sessions;
using Cove.Platform;
using Cove.Platform.Ipc;
using Cove.Platform.Pty;
using Cove.Platform.Pty.Unix;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class HandoffNegotiationTests
{
    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task FailedServe_DoesNotExposeRetryUntilCleanupCompletes()
    {
        using var cleanupEntered = new ManualResetEventSlim();
        using var releaseCleanup = new ManualResetEventSlim();
        using var cleanupCompleted = new ManualResetEventSlim();
        var cleanupCount = 0;
        var listenerReleaseCount = 0;
        var cancellationReleaseCount = 0;
        var socketPathReleaseCount = 0;
        var hooks = new HandoffTransportTestHooks
        {
            ServeCleanupStarting = () =>
            {
                cleanupEntered.Set();
                if (!releaseCleanup.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("handoff cleanup was not released");
            },
            ServeCleanupCompleted = () =>
            {
                Interlocked.Increment(ref cleanupCount);
                cleanupCompleted.Set();
            },
            ListenerReleased = () =>
                Interlocked.Increment(ref listenerReleaseCount),
            CancellationReleased = () =>
                Interlocked.Increment(ref cancellationReleaseCount),
            SocketPathReleased = () =>
                Interlocked.Increment(ref socketPathReleaseCount),
        };
        await using var fixture = await TransportFixture.CreateAsync(hooks);

        var first = fixture.Transport.Begin("first");
        Assert.True(first.Ok);
        var firstResult = first.Data!.Value.Deserialize(
            CoveJsonContext.Default.HandoffBeginResult)!;
        using (var socket = await ConnectAsync(firstResult.SocketPath))
            socket.Send([(byte)'X']);

        Assert.True(
            cleanupEntered.Wait(TimeSpan.FromSeconds(5)),
            "failed serve did not reach cleanup");
        var duringCleanup = fixture.Transport.Begin("during-cleanup");
        releaseCleanup.Set();
        Assert.True(
            cleanupCompleted.Wait(TimeSpan.FromSeconds(5)),
            "failed serve cleanup did not complete");

        Assert.False(duringCleanup.Ok);
        Assert.Equal("conflict", duringCleanup.Error?.Code);
        Assert.Equal(1, Volatile.Read(ref cleanupCount));
        Assert.Equal(1, Volatile.Read(ref listenerReleaseCount));
        Assert.Equal(1, Volatile.Read(ref cancellationReleaseCount));
        Assert.Equal(1, Volatile.Read(ref socketPathReleaseCount));

        var replacement = fixture.Transport.Begin("replacement");
        Assert.True(replacement.Ok);
        var replacementResult = replacement.Data!.Value.Deserialize(
            CoveJsonContext.Default.HandoffBeginResult)!;
        cleanupCompleted.Reset();
        using (var socket = await ConnectAsync(replacementResult.SocketPath))
            socket.Send([(byte)'X']);

        Assert.True(
            cleanupCompleted.Wait(TimeSpan.FromSeconds(5)),
            "replacement serve cleanup did not complete");
        Assert.True(File.Exists(replacementResult.SocketPath) is false);
        Assert.Equal(2, Volatile.Read(ref cleanupCount));
        Assert.Equal(2, Volatile.Read(ref listenerReleaseCount));
        Assert.Equal(2, Volatile.Read(ref cancellationReleaseCount));
        Assert.Equal(2, Volatile.Read(ref socketPathReleaseCount));
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task Begin_WhenDisposeWinsBeforePublication_UnwindsWithoutPublishing()
    {
        using var listenerReady = new ManualResetEventSlim();
        using var releasePublication = new ManualResetEventSlim();
        using var disposeStarted = new ManualResetEventSlim();
        var listenerReleaseCount = 0;
        var cancellationReleaseCount = 0;
        var socketPathReleaseCount = 0;
        var hooks = new HandoffTransportTestHooks
        {
            ListenerReady = () =>
            {
                listenerReady.Set();
                if (!releasePublication.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("handoff publication was not released");
            },
            DisposeStarted = disposeStarted.Set,
            ListenerReleased = () =>
                Interlocked.Increment(ref listenerReleaseCount),
            CancellationReleased = () =>
                Interlocked.Increment(ref cancellationReleaseCount),
            SocketPathReleased = () =>
                Interlocked.Increment(ref socketPathReleaseCount),
        };
        await using var fixture = await TransportFixture.CreateAsync(hooks);

        var beginTask = Task.Run(() => fixture.Transport.Begin("begin"));
        Assert.True(
            listenerReady.Wait(TimeSpan.FromSeconds(5)),
            "begin did not reach the publication barrier");
        var disposeTask = fixture.Transport.DisposeAsync().AsTask();
        Assert.True(
            disposeStarted.Wait(TimeSpan.FromSeconds(5)),
            "dispose did not claim the transport");
        releasePublication.Set();

        var begin = await beginTask.WaitAsync(TimeSpan.FromSeconds(5));
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Transport.DisposeAsync();

        if (begin.Ok)
        {
            var result = begin.Data!.Value.Deserialize(
                CoveJsonContext.Default.HandoffBeginResult)!;
            using var socket = await ConnectAsync(result.SocketPath);
            socket.Send([(byte)'X']);
        }
        Assert.False(begin.Ok);
        Assert.Equal("disposed", begin.Error?.Code);
        Assert.False(File.Exists(fixture.SocketPath));
        Assert.False(fixture.ShutdownRequested.Task.IsCompleted);
        Assert.Equal(1, Volatile.Read(ref listenerReleaseCount));
        Assert.Equal(1, Volatile.Read(ref cancellationReleaseCount));
        Assert.Equal(1, Volatile.Read(ref socketPathReleaseCount));
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task HandoffBegin_TransfersLiveNooksAndShutsPredecessorDown()
    {
        await using var harness = await DaemonTestHarness.StartAsync();
        var conn = await harness.ConnectAsync("cli");
        var spawn = JsonSerializer.SerializeToElement(
            new SpawnParams("/bin/sh", new[] { "-c", "sleep 300" }, "/tmp"), CoveJsonContext.Default.SpawnParams);
        await conn.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("2", "cove://commands/nook.spawn", spawn)), CancellationToken.None);
        var spawnResponse = ControlCodec.DecodeResponse((await conn.ReadFrameAsync(CancellationToken.None))!.Value.Payload);
        Assert.True(spawnResponse.Ok);
        var browserOpen = JsonSerializer.SerializeToElement(
            new BrowserOpenParams("browser-1", "https://example.com"),
            CoveJsonContext.Default.BrowserOpenParams);
        await conn.WriteFrameAsync(
            FrameType.Request,
            0,
            ControlCodec.Encode(
                new ControlRequest(
                    "browser",
                    "cove://commands/browser.open",
                    browserOpen)),
            CancellationToken.None);
        var browserResponse = ControlCodec.DecodeResponse(
            (await conn.ReadFrameAsync(CancellationToken.None))!.Value.Payload);
        Assert.True(browserResponse.Ok);


        await conn.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("3", "cove://handoff/begin", null)), CancellationToken.None);
        var beginResponse = ControlCodec.DecodeResponse((await conn.ReadFrameAsync(CancellationToken.None))!.Value.Payload);
        Assert.True(beginResponse.Ok);
        var begin = beginResponse.Data!.Value.Deserialize(CoveJsonContext.Default.HandoffBeginResult)!;
        Assert.Equal(1, begin.NookCount);
        var browserState = Assert.Single(begin.BrowserNooks!);
        Assert.Equal("browser-1", browserState.NookId);
        Assert.Equal("https://example.com", browserState.CurrentUrl);
        Assert.Equal(["https://example.com"], browserState.History);
        Assert.Equal(0, browserState.HistoryIndex);

        var received = new List<(HandoffNookRecord Record, int Fd, byte[] Ring)>();
        using (var socket = await ConnectAsync(begin.SocketPath))
        {
            var socketFd = (int)socket.Handle;
            for (var i = 0; i < begin.NookCount; i++)
            {
                var item = await Task
                    .Run(() => HandoffWire.ReadRecord(socketFd))
                    .WaitAsync(TimeSpan.FromSeconds(5));
                Assert.NotNull(item);
                received.Add(item!.Value);
            }
            socket.Send(new[] { (byte)'K' });
        }

        Assert.Equal("/bin/sh", received[0].Record.Command);
        Assert.True(received[0].Record.Pid > 0);
        Assert.True(received[0].Fd >= 0);
        foreach (var item in received)
            UnixFdChannel.CloseFd(item.Fd);

        await harness.Run.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task HandoffBegin_SecondCallConflicts()
    {
        await using var harness = await DaemonTestHarness.StartAsync();
        var conn = await harness.ConnectAsync("cli");
        await conn.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("2", "cove://handoff/begin", null)), CancellationToken.None);
        var first = ControlCodec.DecodeResponse((await conn.ReadFrameAsync(CancellationToken.None))!.Value.Payload);
        Assert.True(first.Ok);

        await conn.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("3", "cove://handoff/begin", null)), CancellationToken.None);
        var second = ControlCodec.DecodeResponse((await conn.ReadFrameAsync(CancellationToken.None))!.Value.Payload);
        Assert.False(second.Ok);
        Assert.Equal("conflict", second.Error?.Code);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task AdoptTakenOverNooks_RestoresBrowserState()
    {
        await using var fixture = await TransportFixture.CreateAsync(
            new HandoffTransportTestHooks());
        var takeover = new HandoffTakeover(
            Array.Empty<HandoffTakeoverItem>(),
            [
                new HandoffBrowserNookDto(
                    "browser-1",
                    "https://b.example",
                    [
                        "https://a.example",
                        "https://b.example",
                        "https://c.example",
                    ],
                    1),
            ]);

        var adopted = fixture.Transport.AdoptTakenOverNooks(takeover);

        Assert.Empty(adopted);
        var browser = Assert.IsType<BrowserNook>(
            fixture.Browser.Get("browser-1"));
        Assert.Equal("https://b.example", browser.CurrentUrl);
        Assert.Equal(1, browser.HistoryIndex);
        Assert.True(browser.CanGoBack);
        Assert.True(browser.CanGoForward);
    }

    private static async Task<Socket> ConnectAsync(string socketPath)
    {
        var socket = new Socket(
            AddressFamily.Unix,
            SocketType.Stream,
            ProtocolType.Unspecified);
        socket.SendTimeout = 5_000;
        try
        {
            await Task
                .Run(() => socket.Connect(
                    new UnixDomainSocketEndPoint(socketPath)))
                .WaitAsync(TimeSpan.FromSeconds(5));
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private sealed class TransportFixture : IAsyncDisposable
    {
        private readonly string _root;
        private readonly ProcessEnvironmentScope _environment;
        private readonly NookRegistry _nooks;
        private readonly BrowserNookManager _browser;

        private TransportFixture(
            string root,
            ProcessEnvironmentScope environment,
            NookRegistry nooks,
            BrowserNookManager browser,
            HandoffTransport transport,
            string socketPath,
            TaskCompletionSource shutdownRequested)
        {
            _root = root;
            _environment = environment;
            _nooks = nooks;
            _browser = browser;
            Transport = transport;
            SocketPath = socketPath;
            ShutdownRequested = shutdownRequested;
        }

        public HandoffTransport Transport { get; }
        public BrowserNookManager Browser => _browser;
        public string SocketPath { get; }
        public TaskCompletionSource ShutdownRequested { get; }

        public static async Task<TransportFixture> CreateAsync(
            HandoffTransportTestHooks hooks)
        {
            var root = TestDirectory.Create(
                "handoff-ownership-",
                OperatingSystem.IsWindows() ? null : "/tmp");
            var environment = await ProcessEnvironmentScope.SetAsync(
                "COVE_DATA_DIR",
                root);
            var dataDir = CoveDataDir.Resolve(CoveChannel.Dev);
            CoveTree.Ensure(dataDir);
            var paths = new DaemonPaths(dataDir);
            var nooks = new NookRegistry(
                new EmptyPtyHost(),
                NullLogger.Instance);
            var browser = new BrowserNookManager();
            var shutdownRequested = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var transport = new HandoffTransport(
                paths,
                nooks,
                browser,
                new HookEventRouter(),
                new AgentMessageRouter(),
                new SessionResumeOrchestrator(),
                new EngineEventRouter(CancellationToken.None),
                NullLogger.Instance,
                () => shutdownRequested.TrySetResult(),
                hooks);
            return new TransportFixture(
                root,
                environment,
                nooks,
                browser,
                transport,
                Path.Combine(dataDir.IpcDir, "handoff.sock"),
                shutdownRequested);
        }

        public async ValueTask DisposeAsync()
        {
            await Transport.DisposeAsync();
            _nooks.Dispose();
            await _environment.DisposeAsync();
            TestDirectory.Delete(_root);
        }
    }

    private sealed class EmptyPtyHost : IPtyHost
    {
        public bool IsSupported => true;

        public IPtySession Spawn(PtySpawnRequest request)
        {
            throw new NotSupportedException();
        }
    }
}
