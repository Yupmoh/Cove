using System.Text.Json;
using Cove.Engine;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class PanePrefixResolutionTests
{
    private static PaneRegistry NewPanes()
        => new(PtyHostFactory.Create(NullLogger.Instance), NullLogger.Instance);

    private static string SpawnPane(PaneRegistry panes)
    {
        var req = new ControlRequest("1", "cove://commands/pane.spawn",
            JsonDocument.Parse("""{"command":"/bin/sh","args":["-c","sleep 30"]}""").RootElement.Clone());
        var resp = EngineCommandRouter.RouteAsync(req, panes: panes).GetAwaiter().GetResult();
        return resp!.Data!.Value.GetProperty("paneId").GetString()!;
    }

    [Fact]
    public async Task PaneKill_UniquePrefix_Resolves()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var panes = NewPanes();
        var pane = SpawnPane(panes);
        var prefix = pane.Substring(0, 8);
        var prm = JsonDocument.Parse($"{{\"paneId\":\"{prefix}\"}}").RootElement.Clone();
        var request = new ControlRequest("1", "cove://commands/pane.kill", prm);
        var response = await EngineCommandRouter.RouteAsync(request, panes: panes);
        Assert.NotNull(response);
        Assert.True(response!.Ok);
        Assert.DoesNotContain(panes.List(), p => p.PaneId == pane);
    }

    [Fact]
    public async Task PaneRename_UniquePrefix_Resolves()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var panes = NewPanes();
        string target = "", other = "";
        try
        {
            target = SpawnPane(panes);
            other = SpawnPane(panes);
            var prefix = target.Substring(0, 13);
            var prm = JsonDocument.Parse($"{{\"paneId\":\"{prefix}\",\"title\":\"renamed\"}}").RootElement.Clone();
            var request = new ControlRequest("1", "cove://commands/pane.rename", prm);
            var response = await EngineCommandRouter.RouteAsync(request, panes: panes);
            Assert.NotNull(response);
            Assert.True(response!.Ok);
            var info = System.Linq.Enumerable.First(panes.List(), p => p.PaneId == target);
            Assert.Equal("renamed", info.Title);
        }
        finally
        {
            try { panes.Kill(target); } catch { }
            try { panes.Kill(other); } catch { }
        }
    }

    [Fact]
    public async Task PaneWrite_UniquePrefix_Resolves()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var panes = NewPanes();
        string pane = "";
        try
        {
            pane = SpawnPane(panes);
            var prefix = pane.Substring(0, 8);
            var prm = JsonDocument.Parse($"{{\"paneId\":\"{prefix}\",\"dataBase64\":\"aGk=\"}}").RootElement.Clone();
            var request = new ControlRequest("1", "cove://commands/pane.write", prm);
            var response = await EngineCommandRouter.RouteAsync(request, panes: panes);
            Assert.NotNull(response);
            Assert.True(response!.Ok);
        }
        finally { try { panes.Kill(pane); } catch { } }
    }

    [Fact]
    public async Task PaneResize_UniquePrefix_Resolves()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var panes = NewPanes();
        string pane = "";
        try
        {
            pane = SpawnPane(panes);
            var prefix = pane.Substring(0, 8);
            var prm = JsonDocument.Parse($"{{\"paneId\":\"{prefix}\",\"cols\":100,\"rows\":40}}").RootElement.Clone();
            var request = new ControlRequest("1", "cove://commands/pane.resize", prm);
            var response = await EngineCommandRouter.RouteAsync(request, panes: panes);
            Assert.NotNull(response);
            Assert.True(response!.Ok);
        }
        finally { try { panes.Kill(pane); } catch { } }
    }

    [Fact]
    public async Task PaneWrite_AmbiguousPrefix_ReturnsAmbiguousId()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var panes = NewPanes();
        string a = "", b = "";
        try
        {
            a = SpawnPane(panes);
            b = SpawnPane(panes);
            var prm = JsonDocument.Parse("{\"paneId\":\"pane-\",\"dataBase64\":\"aGk=\"}").RootElement.Clone();
            var request = new ControlRequest("1", "cove://commands/pane.write", prm);
            var response = await EngineCommandRouter.RouteAsync(request, panes: panes);
            Assert.NotNull(response);
            Assert.False(response!.Ok);
            Assert.Equal("ambiguous_id", response.Error?.Code);
        }
        finally
        {
            try { panes.Kill(a); } catch { }
            try { panes.Kill(b); } catch { }
        }
    }

    [Fact]
    public async Task PaneKill_UnknownPrefix_ReturnsNotFound()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var panes = NewPanes();
        string pane = "";
        try
        {
            pane = SpawnPane(panes);
            var prm = JsonDocument.Parse("{\"paneId\":\"zzz-nope\"}").RootElement.Clone();
            var request = new ControlRequest("1", "cove://commands/pane.kill", prm);
            var response = await EngineCommandRouter.RouteAsync(request, panes: panes);
            Assert.NotNull(response);
            Assert.False(response!.Ok);
            Assert.Equal("not_found", response.Error?.Code);
        }
        finally { try { panes.Kill(pane); } catch { } }
    }
}
