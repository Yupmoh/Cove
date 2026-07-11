using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NookDescriptorTests
{
    [Fact]
    public void Descriptors_ReflectsSpawnedNook()
    {
        if (System.OperatingSystem.IsWindows())
            return;

        using var reg = NewRegistry();
        var info = reg.Spawn(new SpawnParams("/bin/sh", new[] { "-c", "sleep 1" }, "/tmp", null, 80, 24));
        var d = reg.Descriptors();
        Assert.Single(d);
        Assert.Equal(info.NookId, d[0].NookId);
        Assert.Equal("/bin/sh", d[0].Command);
    }

    [Fact]
    public void RespawnAs_RegistersUnderGivenId()
    {
        if (System.OperatingSystem.IsWindows())
            return;

        using var reg = NewRegistry();
        reg.RespawnAs("nook-fixed", "/bin/sh", new[] { "-c", "sleep 1" }, "/tmp", 80, 24);
        Assert.Contains(reg.List(), x => x.NookId == "nook-fixed");
    }

    private static NookRegistry NewRegistry()
    {
        var host = PtyHostFactory.Create(NullLogger.Instance);
        return new NookRegistry(host, NullLogger.Instance);
    }
}
