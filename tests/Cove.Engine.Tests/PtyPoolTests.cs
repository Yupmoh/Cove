using Cove.Engine.Nooks;
using Cove.Platform.Pty;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class PtyPoolTests
{
    private sealed class FakePtySession : IPtySession
    {
        public long SessionId { get; }
        public bool HasExited { get; set; }
        public int ExitCode { get; set; }
        public bool IsDisposed { get; private set; }
        public FakePtySession(long id) { SessionId = id; }
        public int Read(Span<byte> buffer) => 0;
        public void Write(ReadOnlySpan<byte> data) { }
        public void Resize(int cols, int rows) { }
        public void Kill() { HasExited = true; }
        public bool Signal(int signum) { HasExited = true; return true; }
        public int WaitForExit() { return ExitCode; }
        public void Dispose() { IsDisposed = true; }
    }

    private sealed class FakePtyHost : IPtyHost
    {
        public bool IsSupported => true;
        public List<FakePtySession> Spawned { get; } = new();
        public Func<bool> ShouldExit { get; set; } = () => false;
        public bool ThrowOnSpawn { get; set; }

        public IPtySession Spawn(PtySpawnRequest request)
        {
            if (ThrowOnSpawn) throw new InvalidOperationException("spawn failed");
            var session = new FakePtySession(Spawned.Count + 1) { HasExited = ShouldExit() };
            Spawned.Add(session);
            return session;
        }
    }

    private static PtySpawnRequest Req() => new() { Command = "/bin/bash", Args = [] };

    [Fact]
    public void PreWarm_AddsSessionToPool()
    {
        var host = new FakePtyHost();
        var pool = new PtyPool(host, NullLogger.Instance);
        pool.PreWarm("default", Req());
        Assert.Equal(1, pool.PoolSize("default"));
    }

    [Fact]
    public void PreWarm_RespectsMaxPoolSize()
    {
        var host = new FakePtyHost();
        var pool = new PtyPool(host, NullLogger.Instance, maxPoolSize: 2);
        pool.PreWarm("default", Req());
        pool.PreWarm("default", Req());
        pool.PreWarm("default", Req());
        Assert.Equal(2, pool.PoolSize("default"));
    }

    [Fact]
    public void TryAcquire_ReturnsPooledSession()
    {
        var host = new FakePtyHost();
        var pool = new PtyPool(host, NullLogger.Instance);
        pool.PreWarm("default", Req());
        var session = pool.TryAcquire("default");
        Assert.NotNull(session);
        Assert.Equal(0, pool.PoolSize("default"));
    }

    [Fact]
    public void TryAcquire_EmptyPool_ReturnsNull()
    {
        var host = new FakePtyHost();
        var pool = new PtyPool(host, NullLogger.Instance);
        Assert.Null(pool.TryAcquire("default"));
    }

    [Fact]
    public void AcquireOrSpawn_UsesPoolWhenAvailable()
    {
        var host = new FakePtyHost();
        var pool = new PtyPool(host, NullLogger.Instance);
        pool.PreWarm("default", Req());
        var session = pool.AcquireOrSpawn("default", Req());
        Assert.NotNull(session);
        Assert.Single(host.Spawned);
    }

    [Fact]
    public void AcquireOrSpawn_SpawnsOnDemandWhenPoolEmpty()
    {
        var host = new FakePtyHost();
        var pool = new PtyPool(host, NullLogger.Instance);
        var session = pool.AcquireOrSpawn("default", Req());
        Assert.NotNull(session);
        Assert.Single(host.Spawned);
    }

    [Fact]
    public void TryAcquire_DiscardsExitedSession()
    {
        var host = new FakePtyHost { ShouldExit = () => true };
        var pool = new PtyPool(host, NullLogger.Instance);
        pool.PreWarm("default", Req());
        Assert.Null(pool.TryAcquire("default"));
        Assert.True(host.Spawned[0].IsDisposed);
    }

    [Fact]
    public void TryAcquire_DiscardsStaleSession()
    {
        var host = new FakePtyHost();
        var pool = new PtyPool(host, NullLogger.Instance, maxWarmAge: TimeSpan.FromMilliseconds(1));
        pool.PreWarm("default", Req());
        System.Threading.Thread.Sleep(10);
        Assert.Null(pool.TryAcquire("default"));
    }

    [Fact]
    public void DrainPool_DisposesAllSessions()
    {
        var host = new FakePtyHost();
        var pool = new PtyPool(host, NullLogger.Instance);
        pool.PreWarm("default", Req());
        pool.PreWarm("default", Req());
        pool.DrainPool("default");
        Assert.Equal(0, pool.PoolSize("default"));
        Assert.True(host.Spawned[0].IsDisposed);
        Assert.True(host.Spawned[1].IsDisposed);
    }

    [Fact]
    public void DrainAll_DisposesAllPools()
    {
        var host = new FakePtyHost();
        var pool = new PtyPool(host, NullLogger.Instance);
        pool.PreWarm("a", Req());
        pool.PreWarm("b", Req());
        pool.DrainAll();
        Assert.Equal(0, pool.PoolSize("a"));
        Assert.Equal(0, pool.PoolSize("b"));
    }

    [Fact]
    public void PreWarm_SpawnFailure_DoesNotThrow()
    {
        var host = new FakePtyHost { ThrowOnSpawn = true };
        var pool = new PtyPool(host, NullLogger.Instance);
        pool.PreWarm("default", Req());
        Assert.Equal(0, pool.PoolSize("default"));
    }

    [Fact]
    public void PreWarm_EmptyKey_DoesNothing()
    {
        var host = new FakePtyHost();
        var pool = new PtyPool(host, NullLogger.Instance);
        pool.PreWarm("", Req());
        Assert.Empty(host.Spawned);
    }

    [Fact]
    public void MultiplePools_TrackedSeparately()
    {
        var host = new FakePtyHost();
        var pool = new PtyPool(host, NullLogger.Instance);
        pool.PreWarm("a", Req());
        pool.PreWarm("b", Req());
        Assert.Equal(1, pool.PoolSize("a"));
        Assert.Equal(1, pool.PoolSize("b"));
    }
}
