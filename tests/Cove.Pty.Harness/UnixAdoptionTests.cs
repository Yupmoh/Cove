using System.Text;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Cove.Platform.Pty.Unix;
using Cove.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Pty.Harness;

public sealed class UnixAdoptionTests
{
    private static IPtySession AdoptOwned(IPtyHost host, int masterFd, int pid)
    {
        var ownedFd = UnixFd.Duplicate(masterFd);
        try
        {
            return host.AdoptSession(ownedFd, pid);
        }
        catch
        {
            UnixFdChannel.CloseFd(ownedFd);
            throw;
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task AdoptedSession_ContinuesTheSameProcess()
    {
        string shell = File.Exists("/bin/zsh") ? "/bin/zsh" : "/bin/bash";
        var logger = NullLogger.Instance;
        var predecessor = PtyHostFactory.Create(logger);
        using var original = predecessor.Spawn(new PtySpawnRequest
        {
            Command = shell,
            Args = new[] { "-i" },
            Cols = 80,
            Rows = 24,
        });

        Assert.True(predecessor.TryExportSession(original, out var masterFd, out var pid));
        Assert.True(masterFd >= 0);
        Assert.True(pid > 0);

        var successor = PtyHostFactory.Create(logger);
        var adopted = AdoptOwned(successor, masterFd, pid);
        var ring = new PtyRingBuffer(1 << 20);
        var signal = new PtyRingSignal();
        var reader = new PtySessionReader(adopted, ring, signal, logger);
        reader.Start();
        try
        {
            var cursor = new PtyClientCursor();
            var sink = new RecordingSink();
            var delivery = new PtyDelivery(adopted.SessionId, ring, cursor, sink);
            adopted.Write(Encoding.UTF8.GetBytes("printf 'COVE_ADOPT_%s\\n' OK\n"));
            await AsyncTest.EventuallyAsync(
                () =>
                {
                    delivery.PumpAvailable();
                    return sink.Contains("COVE_ADOPT_OK"u8);
                },
                TimeSpan.FromSeconds(10),
                "adopted shell never emitted the marker");
            adopted.Write(Encoding.UTF8.GetBytes("exit\n"));

            await AsyncTest.EventuallyAsync(
                () => reader.HasCompleted,
                TimeSpan.FromSeconds(15),
                "adopted shell reader never completed");
            delivery.PumpAvailable();
            Assert.Contains("COVE_ADOPT_OK", Encoding.UTF8.GetString(sink.Delivered));
        }
        finally
        {
            reader.Dispose();
            adopted.Dispose();
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public void AdoptSession_RejectsUnusableFd()
    {
        var host = PtyHostFactory.Create(NullLogger.Instance);
        Assert.Throws<ArgumentOutOfRangeException>(() => host.AdoptSession(1_000_000, 1));
    }


    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task AdoptedSession_ObservesProcessExit()
    {
        var logger = NullLogger.Instance;
        var predecessor = PtyHostFactory.Create(logger);
        using var original = predecessor.Spawn(new PtySpawnRequest
        {
            Command = "/bin/sh",
            Args = new[] { "-c", "sleep 300" },
            Cols = 80,
            Rows = 24,
        });
        Assert.True(predecessor.TryExportSession(original, out var masterFd, out var pid));
        var successor = PtyHostFactory.Create(logger);
        var adopted = AdoptOwned(successor, masterFd, pid);
        try
        {
            adopted.Kill();
            var exit = await Task.Run(adopted.WaitForExit).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(OperatingSystem.IsMacOS() ? 137 : -1, exit);
            Assert.True(adopted.HasExited);
        }
        finally
        {
            adopted.Dispose();
        }
    }

    [PlatformFact(TestOperatingSystem.MacOS)]
    public async Task AdoptedSession_ReportsExitCodeWhenWaitComesLate()
    {
        var logger = NullLogger.Instance;
        var predecessor = PtyHostFactory.Create(logger);
        using var original = predecessor.Spawn(new PtySpawnRequest
        {
            Command = "/bin/sh",
            Args = new[] { "-c", "sleep 0.4; exit 5" },
            Cols = 80,
            Rows = 24,
        });
        Assert.True(predecessor.TryExportSession(original, out var masterFd, out var pid));
        var successor = PtyHostFactory.Create(logger);
        var adopted = AdoptOwned(successor, masterFd, pid);
        var originalExitCode = -1;
        try
        {
            var observedStatus = await ProcessExitWatch.WaitForExitAsync(pid)
                .WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(5, ProcessExitWatch.DecodeWaitStatus(observedStatus));
            Assert.Equal(5, await Task.Run(adopted.WaitForExit).WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            adopted.Dispose();
            originalExitCode = await Task.Run(original.WaitForExit).WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(5, originalExitCode);
    }

    [Fact]
    public void FakeHostsWithoutHandoff_DeclineExport()
    {
        var host = new NoHandoffHost();
        Assert.False(((IPtyHost)host).TryExportSession(null!, out var fd, out var pid));
        Assert.Equal(-1, fd);
        Assert.Equal(-1, pid);
        Assert.Throws<PlatformNotSupportedException>(() => ((IPtyHost)host).AdoptSession(3, 42));
    }

    private sealed class NoHandoffHost : IPtyHost
    {
        public bool IsSupported => true;
        public IPtySession Spawn(PtySpawnRequest request) => throw new NotSupportedException();
    }
}
