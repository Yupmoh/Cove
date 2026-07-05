using System.IO;
using System.Text;
using Cove.Engine.Layout;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ScrollbackRestoreTests
{
    [Fact]
    public void SaveLoad_Scrollback_RoundTrips()
    {
        var wsDir = Path.Combine(Path.GetTempPath(), "covescroll-" + System.Guid.NewGuid().ToString("N"));
        try
        {
            WorkspacePersistence.SaveScrollback("p1", new byte[] { 1, 2, 3, 4 }, wsDir);
            var b = WorkspacePersistence.LoadScrollback("p1", wsDir);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, b);
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
        Assert.Null(WorkspacePersistence.LoadScrollback("nope", Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public void RespawnAs_PreseedsPriorScrollback()
    {
        if (System.OperatingSystem.IsWindows())
            return;

        var host = PtyHostFactory.Create(NullLogger.Instance);
        var reg = new PaneRegistry(host, NullLogger.Instance);
        try
        {
            var prior = Encoding.UTF8.GetBytes("PRIOR_OUTPUT\n");
            reg.RespawnAs("pane-x", "/bin/sh", new[] { "-c", "sleep 1" }, "/tmp", 80, 24, prior);
            System.Threading.Thread.Sleep(150);
            var snap = reg.SnapshotRing("pane-x");
            Assert.Contains("PRIOR_OUTPUT", Encoding.UTF8.GetString(snap));
        }
        finally
        {
            reg.Dispose();
        }
    }
}
