using System.Linq;
using System.Text.Json;
using Cove.Engine;
using Cove.Engine.Pty;
using Cove.Persistence;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NookRouteTests
{
    private static NookRegistry NewRegistry()
    {
        var host = PtyHostFactory.Create(NullLogger.Instance);
        return new NookRegistry(host, NullLogger.Instance);
    }

    [Fact]
    public void Rename_UpdatesTitle()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var reg = NewRegistry();
        var info = reg.Spawn(new SpawnParams("/bin/sh", new[] { "-c", "sleep 30" }, "/tmp", null, 40, 10));
        Assert.True(reg.Rename(info.NookId, "my nook"));
        var after = reg.List().First(p => p.NookId == info.NookId);
        Assert.Equal("my nook", after.Title);
    }

    [Fact]
    public void Rename_UnknownNook_ReturnsFalse()
    {
        using var reg = NewRegistry();
        Assert.False(reg.Rename("nope", "x"));
    }

    [Fact]
    public void Read_ReturnsBytesFromOffset()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var reg = NewRegistry();
        var info = reg.Spawn(new SpawnParams("/bin/sh", new[] { "-c", "sleep 30" }, "/tmp", null, 40, 10));
        reg.Write(info.NookId, System.Text.Encoding.UTF8.GetBytes("echo hello\n"));
        System.Threading.Thread.Sleep(300);
        var bytes = reg.Read(info.NookId, 0, 4096);
        Assert.NotEmpty(bytes);
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("hello", text);
    }

    [Fact]
    public void Read_UnknownNook_ReturnsEmpty()
    {
        using var reg = NewRegistry();
        Assert.Empty(reg.Read("nope", 0, 64));
    }
}

public sealed class LayoutSnapshotRouteTests
{
    [Fact]
    public async Task Snapshot_Route_ReturnsBaySnapshot()
    {
        var layout = new Cove.Engine.Layout.LayoutService();
        layout.CreateShore("main", new NookLeaf { NookId = "p1", Subtabs = new[] { new Subtab("p1", NookType.Terminal) } });
        var request = new ControlRequest("r1", "cove://commands/layout.snapshot");
        var resp = await EngineCommandRouter.RouteAsync(request, layout: layout);
        Assert.NotNull(resp);
        Assert.True(resp!.Ok);
        Assert.NotNull(resp.Data);
        var snap = resp.Data!.Value.Deserialize(Cove.Persistence.CoveJsonContext.Default.BaySnapshot);
        Assert.NotNull(snap);
        Assert.NotEmpty(snap!.Shores);
    }
}
