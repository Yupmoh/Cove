using System.IO;
using System.Text;
using Cove.Engine.Layout;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class ScrollbackRestoreTests
{
    [Fact]
    public void SaveLoad_Scrollback_RoundTrips()
    {
        var wsDir = Path.Combine(Path.GetTempPath(), "covescroll-" + System.Guid.NewGuid().ToString("N"));
        try
        {
            BayPersistence.SaveScrollback("p1", new byte[] { 1, 2, 3, 4 }, wsDir);
            var b = BayPersistence.LoadScrollback("p1", wsDir);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, b);
        }
        finally
        {
            if (Directory.Exists(wsDir))
                Directory.Delete(wsDir, recursive: true);
        }
    }

    [Fact]
    public void SaveLoad_TerminalRestoreState_RoundTripsMetadataAndBytes()
    {
        var wsDir = Path.Combine(Path.GetTempPath(), "coveterminalstate-" + System.Guid.NewGuid().ToString("N"));
        try
        {
            var state = new TerminalRestoreState(Encoding.UTF8.GetBytes("STATE"), Encoding.UTF8.GetBytes("TAIL"), 42, 132, 40, 10000, "\x1b[?1006h");
            BayPersistence.SaveTerminalRestoreState("p1", state, wsDir);

            var restored = Assert.IsType<TerminalRestoreState>(BayPersistence.LoadTerminalRestoreState("p1", wsDir, NullLogger.Instance));
            Assert.Equal(state.Checkpoint, restored.Checkpoint);
            Assert.Equal(state.Tail, restored.Tail);
            Assert.Equal(state.Offset, restored.Offset);
            Assert.Equal(state.Cols, restored.Cols);
            Assert.Equal(state.Rows, restored.Rows);
            Assert.Equal(state.ScrollbackLines, restored.ScrollbackLines);
            Assert.Equal(state.ModeSupplement, restored.ModeSupplement);
        }
        finally
        {
            if (Directory.Exists(wsDir))
                Directory.Delete(wsDir, recursive: true);
        }
    }

    [Fact]
    public void CorruptTerminalRestoreState_DegradesToRawScrollback()
    {
        var wsDir = Path.Combine(Path.GetTempPath(), "covecorruptstate-" + System.Guid.NewGuid().ToString("N"));
        try
        {
            var nookDir = Path.Combine(wsDir, "nooks", "p1");
            Directory.CreateDirectory(nookDir);
            File.WriteAllBytes(Path.Combine(nookDir, "terminal-state.bin"), Encoding.ASCII.GetBytes("corrupt"));
            File.WriteAllBytes(Path.Combine(nookDir, "scrollback.bin"), Encoding.ASCII.GetBytes("RAW_FALLBACK"));

            Assert.Null(BayPersistence.LoadTerminalRestoreState("p1", wsDir, NullLogger.Instance));
            Assert.Equal("RAW_FALLBACK", Encoding.ASCII.GetString(BayPersistence.LoadScrollback("p1", wsDir)!));
        }
        finally
        {
            if (Directory.Exists(wsDir))
                Directory.Delete(wsDir, recursive: true);
        }
    }

    [Fact]
    public void LoadScrollback_Missing_ReturnsNull()
    {
        Assert.Null(BayPersistence.LoadScrollback("nope", Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"))));
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public void RespawnAs_PreseedsPriorScrollback()
    {

        var host = PtyHostFactory.Create(NullLogger.Instance);
        var reg = new NookRegistry(host, NullLogger.Instance);
        try
        {
            var prior = Encoding.UTF8.GetBytes("PRIOR_OUTPUT\n");
            reg.RespawnAs("nook-x", "/bin/sh", new[] { "-c", "sleep 1" }, "/tmp", 80, 24, prior);
            var snap = reg.SnapshotRing("nook-x");
            Assert.Contains("PRIOR_OUTPUT", Encoding.UTF8.GetString(snap));
        }
        finally
        {
            reg.Dispose();
        }
    }
    [PlatformFact(TestOperatingSystem.Unix)]
    public void SnapshotRing_PreservesEntireRetainedRing()
    {

        var host = PtyHostFactory.Create(NullLogger.Instance);
        var reg = new NookRegistry(host, NullLogger.Instance);
        try
        {
            var prior = new byte[1024 * 1024];
            for (var index = 0; index < prior.Length; index++)
                prior[index] = (byte)(index % 251);

            reg.RespawnAs("nook-full", "/bin/sh", new[] { "-c", "sleep 1" }, "/tmp", 80, 24, prior);

            Assert.Equal(prior, reg.SnapshotRing("nook-full"));
        }
        finally
        {
            reg.Dispose();
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public void CaptureTerminalRestoreState_KeepsCheckpointAndRawTailSeparate()
    {

        var host = PtyHostFactory.Create(NullLogger.Instance);
        var reg = new NookRegistry(host, NullLogger.Instance);
        try
        {
            var prior = Encoding.UTF8.GetBytes("abcdefghij");
            reg.RespawnAs("nook-checkpoint", "/bin/sh", new[] { "-c", "sleep 1" }, "/tmp", 80, 24, prior);

            Assert.True(reg.StoreTerminalCheckpoint("nook-checkpoint", Encoding.UTF8.GetBytes("STATE"), 4, 80, 24, 10000));
            var state = Assert.IsType<TerminalRestoreState>(reg.CaptureTerminalRestoreState("nook-checkpoint"));
            Assert.Equal("STATE", Encoding.UTF8.GetString(state.Checkpoint));
            Assert.Equal("efghij", Encoding.UTF8.GetString(state.Tail));
            Assert.Equal(4, state.Offset);
            Assert.Equal(80, state.Cols);
            Assert.Equal(24, state.Rows);
            Assert.Equal(10000, state.ScrollbackLines);
            Assert.Equal(prior, reg.SnapshotRing("nook-checkpoint"));
        }
        finally
        {
            reg.Dispose();
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public void RespawnAs_TerminalRestoreState_NormalizesCheckpointToRestoredTail()
    {
        var host = PtyHostFactory.Create(NullLogger.Instance);
        var reg = new NookRegistry(host, NullLogger.Instance);
        try
        {
            var restored = new TerminalRestoreState(Encoding.UTF8.GetBytes("STATE"), Encoding.UTF8.GetBytes("TAIL"), 9000, 132, 40, 10000, "\x1b[?1006h");
            reg.RespawnAs("nook-restored-checkpoint", "/bin/sh", new[] { "-c", "sleep 1" }, "/tmp", 80, 24, restored);

            var captured = Assert.IsType<TerminalRestoreState>(reg.CaptureTerminalRestoreState("nook-restored-checkpoint"));
            Assert.Equal("STATE", Encoding.UTF8.GetString(captured.Checkpoint));
            Assert.Equal("TAIL", Encoding.UTF8.GetString(captured.Tail));
            Assert.Equal(0, captured.Offset);
            Assert.Equal(132, captured.Cols);
            Assert.Equal(40, captured.Rows);
            Assert.Equal("\x1b[?1006h", captured.ModeSupplement);
        }
        finally
        {
            reg.Dispose();
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public void StoreTerminalCheckpoint_RejectsOffsetOutsideRetainedRange()
    {

        var host = PtyHostFactory.Create(NullLogger.Instance);
        var reg = new NookRegistry(host, NullLogger.Instance);
        try
        {
            var prior = Encoding.UTF8.GetBytes("abcdefghij");
            reg.RespawnAs("nook-invalid-checkpoint", "/bin/sh", new[] { "-c", "sleep 1" }, "/tmp", 80, 24, prior);

            Assert.False(reg.StoreTerminalCheckpoint("nook-invalid-checkpoint", Encoding.UTF8.GetBytes("STATE"), 11, 80, 24, 10000));
            Assert.Equal(prior, reg.SnapshotRing("nook-invalid-checkpoint"));
        }
        finally
        {
            reg.Dispose();
        }
    }
    [PlatformFact(TestOperatingSystem.Unix)]
    public void StoreTerminalCheckpoint_UsesInclusiveRingCapacityBoundary()
    {
        var host = PtyHostFactory.Create(NullLogger.Instance);
        var reg = new NookRegistry(host, NullLogger.Instance);
        try
        {
            reg.RespawnAs("nook-boundary-valid", "/bin/sh", new[] { "-c", "sleep 1" }, "/tmp", 80, 24, new byte[PtyConstants.DefaultRingCapacityBytes]);
            Assert.True(reg.StoreTerminalCheckpoint("nook-boundary-valid", Encoding.UTF8.GetBytes("STATE"), 0, 80, 24, 10000));

            reg.RespawnAs("nook-boundary-expired", "/bin/sh", new[] { "-c", "sleep 1" }, "/tmp", 80, 24, new byte[PtyConstants.DefaultRingCapacityBytes + 1]);
            Assert.False(reg.StoreTerminalCheckpoint("nook-boundary-expired", Encoding.UTF8.GetBytes("STATE"), 0, 80, 24, 10000));
        }
        finally
        {
            reg.Dispose();
        }
    }

}
