using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class PaneDescriptorTests
{
    [Fact]
    public void Descriptors_ReflectsSpawnedPane()
    {
        if (System.OperatingSystem.IsWindows())
            return;

        using var reg = NewRegistry();
        var info = reg.Spawn(new SpawnParams("/bin/sh", new[] { "-c", "sleep 1" }, "/tmp", null, 80, 24));
        var d = reg.Descriptors();
        Assert.Single(d);
        Assert.Equal(info.PaneId, d[0].PaneId);
        Assert.Equal("/bin/sh", d[0].Command);
    }

    [Fact]
    public void RespawnAs_RegistersUnderGivenId()
    {
        if (System.OperatingSystem.IsWindows())
            return;

        using var reg = NewRegistry();
        reg.RespawnAs("pane-fixed", "/bin/sh", new[] { "-c", "sleep 1" }, "/tmp", 80, 24);
        Assert.Contains(reg.List(), x => x.PaneId == "pane-fixed");
    }

    private static PaneRegistry NewRegistry()
    {
        var host = PtyHostFactory.Create(NullLogger.Instance);
        return new PaneRegistry(host, NullLogger.Instance);
    }
}
