using System.Text;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NookRegistryOwnershipTests
{
    private sealed class TrackingSession(long sessionId) : IPtySession
    {
        public long SessionId { get; } = sessionId;
        public bool HasExited { get; private set; }
        public int ExitCode => 0;
        public int KillCount { get; private set; }
        public int DisposeCount { get; private set; }
        public int ResizeCount { get; private set; }

        public int Read(Span<byte> buffer) => 0;
        public void Write(ReadOnlySpan<byte> data) { }
        public void Resize(int cols, int rows) => ResizeCount++;

        public void Kill()
        {
            KillCount++;
            HasExited = true;
        }

        public bool Signal(int signum)
        {
            HasExited = true;
            return true;
        }

        public int WaitForExit() => ExitCode;
        public void Dispose() => DisposeCount++;
    }

    private sealed class TrackingHost : IPtyHost
    {
        private long _nextSessionId;

        public bool IsSupported => true;
        public List<TrackingSession> Spawned { get; } = [];
        public List<PtySpawnRequest> Requests { get; } = [];
        public TrackingSession? Adopted { get; private set; }

        public IPtySession Spawn(PtySpawnRequest request)
        {
            Requests.Add(request);
            var session = new TrackingSession(++_nextSessionId);
            Spawned.Add(session);
            return session;
        }

        public IPtySession AdoptSession(int masterFd, int pid)
        {
            Adopted = new TrackingSession(++_nextSessionId);
            return Adopted;
        }
    }

    private static SpawnEnvironment SpawnEnvironment() =>
        new("/usr/bin:/bin", "/tmp/cove-tests", "/usr/bin/cove", "bay-test", "dev");

    [Fact]
    public void Spawn_AllocatesUniqueIdentityAndAuthenticatesOnlyItsToken()
    {
        var host = new TrackingHost();
        using var registry = new NookRegistry(host, NullLogger.Instance, SpawnEnvironment());

        var first = registry.Spawn(new SpawnParams("/bin/sh", [], "/tmp", null, 80, 24));
        var second = registry.Spawn(new SpawnParams("/bin/sh", [], "/tmp", null, 80, 24));
        var firstToken = Assert.IsType<Dictionary<string, string>>(host.Requests[0].Environment)["COVE_NOOK_TOKEN"];
        var secondToken = Assert.IsType<Dictionary<string, string>>(host.Requests[1].Environment)["COVE_NOOK_TOKEN"];

        Assert.NotEqual(first.NookId, second.NookId);
        Assert.NotEqual(firstToken, secondToken);
        Assert.Equal(64, firstToken.Length);
        Assert.Equal(NookAuthResult.Bound, registry.Authenticate(first.NookId, firstToken));
        Assert.Equal(NookAuthResult.Rejected, registry.Authenticate(first.NookId, secondToken));
        Assert.Equal(NookAuthResult.Unknown, registry.Authenticate("nook-missing", firstToken));
    }

    [Fact]
    public void RespawnAs_ReplacesAndDisposesThePriorSessionExactlyOnce()
    {
        var host = new TrackingHost();
        using var registry = new NookRegistry(host, NullLogger.Instance);

        registry.RespawnAs("nook-fixed", "/bin/sh", [], "/tmp", 80, 24);
        registry.RespawnAs("nook-fixed", "/bin/sh", [], "/tmp", 80, 24);

        Assert.Equal(1, host.Spawned[0].KillCount);
        Assert.Equal(1, host.Spawned[0].DisposeCount);
        Assert.Equal(0, host.Spawned[1].DisposeCount);
    }

    [Fact]
    public void TerminalCheckpoint_CapturesCheckpointAndReplayTail()
    {
        var host = new TrackingHost();
        using var registry = new NookRegistry(host, NullLogger.Instance);
        var prior = Encoding.UTF8.GetBytes("abcdef");
        var checkpoint = Encoding.UTF8.GetBytes("screen");

        registry.RespawnAs("nook-state", "/bin/sh", [], "/tmp", 80, 24, prior);

        Assert.True(registry.StoreTerminalCheckpoint("nook-state", checkpoint, 2, 100, 40, 500));
        var state = Assert.IsType<TerminalRestoreState>(registry.CaptureTerminalRestoreState("nook-state"));
        Assert.Equal(checkpoint, state.Checkpoint);
        Assert.Equal("cdef", Encoding.UTF8.GetString(state.Tail));
        Assert.Equal(2, state.Offset);
        Assert.Equal(100, state.Cols);
        Assert.Equal(40, state.Rows);
        Assert.Equal(500, state.ScrollbackLines);
    }

    [Fact]
    public void Adopt_RestoresIdentityReplayAndTransfersDisposalToRegistry()
    {
        var host = new TrackingHost();
        var token = new string('A', 64);
        var checkpoint = Convert.ToBase64String(Encoding.UTF8.GetBytes("screen"));
        var record = new HandoffNookRecord(
            "nook-adopted",
            Environment.ProcessId,
            "/bin/sh",
            [],
            "/tmp",
            "/tmp/work",
            100,
            40,
            "title",
            "adapter",
            "agent",
            12,
            4,
            null,
            null,
            new HandoffCheckpointDto(checkpoint, 10, 100, 40, 500, ""),
            token);
        var registry = new NookRegistry(host, NullLogger.Instance);

        var adopted = registry.Adopt(record, 3, Encoding.UTF8.GetBytes("tail"));

        Assert.NotNull(adopted);
        Assert.Equal(NookAuthResult.Bound, registry.Authenticate(record.NookId, token));
        Assert.True(registry.ConsumePendingRepaint(record.NookId));
        Assert.Equal("tail", Encoding.UTF8.GetString(registry.Read(record.NookId, 8, 4)));
        var state = Assert.IsType<TerminalRestoreState>(registry.CaptureTerminalRestoreState(record.NookId));
        Assert.Equal("il", Encoding.UTF8.GetString(state.Tail));
        Assert.Equal(1, host.Adopted!.ResizeCount);
        Assert.Equal(0, host.Adopted.DisposeCount);

        registry.Dispose();

        Assert.Equal(1, host.Adopted.KillCount);
        Assert.Equal(1, host.Adopted.DisposeCount);
    }

    [Fact]
    public void Dispose_TerminatesEachOwnedSessionExactlyOnce()
    {
        var host = new TrackingHost();
        var registry = new NookRegistry(host, NullLogger.Instance);
        registry.RespawnAs("nook-a", "/bin/sh", [], "/tmp", 80, 24);
        registry.RespawnAs("nook-b", "/bin/sh", [], "/tmp", 80, 24);

        registry.Dispose();
        registry.Dispose();

        Assert.All(host.Spawned, session =>
        {
            Assert.Equal(1, session.KillCount);
            Assert.Equal(1, session.DisposeCount);
        });
    }
}
