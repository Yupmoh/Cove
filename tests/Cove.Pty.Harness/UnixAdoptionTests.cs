using System.Diagnostics;
using System.Text;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Pty.Harness;

public sealed class UnixAdoptionTests
{
    [Fact]
    public void AdoptedSession_ContinuesTheSameProcess()
    {
        if (OperatingSystem.IsWindows()) return;
        string shell = File.Exists("/bin/zsh") ? "/bin/zsh" : "/bin/bash";
        var logger = NullLogger.Instance;
        var predecessor = PtyHostFactory.Create(logger);
        var original = predecessor.Spawn(new PtySpawnRequest
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
        var adopted = successor.AdoptSession(masterFd, pid);
        var ring = new PtyRingBuffer(1 << 20);
        var signal = new PtyRingSignal();
        var reader = new PtySessionReader(adopted, ring, signal, logger);
        reader.Start();
        try
        {
            adopted.Write(Encoding.UTF8.GetBytes("printf 'COVE_ADOPT_%s\\n' OK\n"));
            Thread.Sleep(500);
            adopted.Write(Encoding.UTF8.GetBytes("exit\n"));

            var sw = Stopwatch.StartNew();
            while (!reader.HasCompleted && sw.Elapsed.TotalSeconds < 15)
                Thread.Sleep(5);
            Assert.True(reader.HasCompleted);

            var cursor = new PtyClientCursor();
            var sink = new RecordingSink();
            var delivery = new PtyDelivery(adopted.SessionId, ring, cursor, sink);
            delivery.PumpAvailable();
            Assert.Contains("COVE_ADOPT_OK", Encoding.UTF8.GetString(sink.Delivered));
        }
        finally
        {
            reader.Dispose();
            adopted.Dispose();
        }
    }

    [Fact]
    public void AdoptSession_RejectsUnusableFd()
    {
        if (OperatingSystem.IsWindows()) return;
        var host = PtyHostFactory.Create(NullLogger.Instance);
        Assert.Throws<ArgumentOutOfRangeException>(() => host.AdoptSession(1_000_000, 1));
    }


    [Fact]
    public void AdoptedSession_ObservesProcessExit()
    {
        if (OperatingSystem.IsWindows()) return;
        var logger = NullLogger.Instance;
        var predecessor = PtyHostFactory.Create(logger);
        var original = predecessor.Spawn(new PtySpawnRequest
        {
            Command = "/bin/sh",
            Args = new[] { "-c", "sleep 300" },
            Cols = 80,
            Rows = 24,
        });
        Assert.True(predecessor.TryExportSession(original, out var masterFd, out var pid));
        var successor = PtyHostFactory.Create(logger);
        var adopted = successor.AdoptSession(masterFd, pid);
        try
        {
            adopted.Kill();
            var sw = Stopwatch.StartNew();
            var exit = adopted.WaitForExit();
            Assert.Equal(-1, exit);
            Assert.True(adopted.HasExited);
            Assert.True(sw.Elapsed.TotalSeconds < 10);
        }
        finally
        {
            adopted.Dispose();
        }
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
