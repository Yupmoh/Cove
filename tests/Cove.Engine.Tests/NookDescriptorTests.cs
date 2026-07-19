using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class NookDescriptorTests
{
    [PlatformFact(TestOperatingSystem.Unix)]
    public void Descriptors_ReflectsSpawnedNook()
    {

        using var reg = NewRegistry();
        var info = reg.Spawn(new SpawnParams("/bin/sh", new[] { "-c", "sleep 1" }, "/tmp", null, 80, 24));
        var d = reg.Descriptors();
        Assert.Single(d);
        Assert.Equal(info.NookId, d[0].NookId);
        Assert.Equal("/bin/sh", d[0].Command);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public void Descriptors_CarryLiveDimensions()
    {

        using var reg = NewRegistry();
        reg.Spawn(new SpawnParams("/bin/sh", new[] { "-c", "sleep 1" }, "/tmp", null, 120, 40));
        var d = reg.Descriptors();
        Assert.Single(d);
        Assert.Equal(120, d[0].Cols);
        Assert.Equal(40, d[0].Rows);

        reg.Resize(d[0].NookId, 200, 60);
        var d2 = reg.Descriptors();
        Assert.Equal(200, d2[0].Cols);
        Assert.Equal(60, d2[0].Rows);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public void RespawnAs_RegistersUnderGivenId()
    {

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
