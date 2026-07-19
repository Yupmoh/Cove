using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class DaemonHostTests
{
    [Fact]
    public void CommandNamespaceSessionRoutes_AreGenerated()
    {
        Assert.Contains(
            "cove://commands/window.focus",
            EngineCommandCatalogue.RegisteredRoutes);
        Assert.Contains(
            "cove://commands/restore.summary.get",
            EngineCommandCatalogue.RegisteredRoutes);
        Assert.DoesNotContain(
            "cove://commands/nook.subscribe",
            EngineCommandCatalogue.RegisteredRoutes);
    }

    [Fact]
    public async Task Hello_Then_Ping_Succeeds()
    {
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection conn = await h.ConnectAsync("cli");
        await conn.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("2", "cove://sys/ping")), CancellationToken.None);
        Frame resp = (await conn.ReadFrameAsync(CancellationToken.None))!.Value;
        ControlResponse r = ControlCodec.DecodeResponse(resp.Payload);
        Assert.True(r.Ok);
        Assert.True(r.Data!.Value.GetProperty("pong").GetBoolean());
    }

    [Fact]
    public async Task PreHello_Command_ReturnsNotReady()
    {
        await using var h = await DaemonTestHarness.StartAsync();
        System.IO.Stream s = await h.Endpoint.ConnectAsync(5000, CancellationToken.None);
        await using var conn = new FrameConnection(s);
        await conn.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("1", "cove://sys/ping")), CancellationToken.None);
        Frame resp = (await conn.ReadFrameAsync(CancellationToken.None))!.Value;
        ControlResponse r = ControlCodec.DecodeResponse(resp.Payload);
        Assert.False(r.Ok);
        Assert.Equal("not_ready", r.Error!.Code);
    }

    [Fact]
    public async Task DaemonStatus_ReportsChannelAndPid()
    {
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection conn = await h.ConnectAsync("cli");
        await conn.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("2", "cove://sys/daemon.status")), CancellationToken.None);
        Frame resp = (await conn.ReadFrameAsync(CancellationToken.None))!.Value;
        ControlResponse r = ControlCodec.DecodeResponse(resp.Payload);
        Assert.True(r.Ok);
        JsonElement d = r.Data!.Value;
        Assert.Equal("dev", d.GetProperty("channel").GetString());
        Assert.True(d.GetProperty("pid").GetInt32() > 0);
    }

    [Fact]
    public async Task WindowFocus_NoGui_ReturnsNoRenderClient()
    {
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection conn = await h.ConnectAsync("cli");
        await conn.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("2", "cove://commands/window.focus")), CancellationToken.None);
        Frame resp = (await conn.ReadFrameAsync(CancellationToken.None))!.Value;
        ControlResponse r = ControlCodec.DecodeResponse(resp.Payload);
        Assert.True(r.Ok);
        Assert.False(r.Data!.Value.GetProperty("focused").GetBoolean());
        Assert.Equal("no_render_client", r.Data!.Value.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task WindowFocus_WithGuiClient_ForwardsEvent()
    {
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection gui = await h.ConnectAsync("gui");
        await using FrameConnection cli = await h.ConnectAsync("cli");

        await cli.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("2", "cove://commands/window.focus")), CancellationToken.None);
        Frame cliResp = (await cli.ReadFrameAsync(CancellationToken.None))!.Value;
        ControlResponse r = ControlCodec.DecodeResponse(cliResp.Payload);
        Assert.True(r.Data!.Value.GetProperty("focused").GetBoolean());

        using var evCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Frame ev = (await gui.ReadFrameAsync(evCts.Token))!.Value;
        Assert.Equal(FrameType.Event, ev.Header.Type);
        ControlEvent e = ControlCodec.DecodeEvent(ev.Payload);
        Assert.Equal("window.focus", e.Channel);
    }

    [Fact]
    public async Task DaemonStop_ShutsDownHost()
    {
        await using var h = await DaemonTestHarness.StartAsync();
        await using (FrameConnection conn = await h.ConnectAsync("cli"))
        {
            await conn.WriteFrameAsync(FrameType.Request, 0,
                ControlCodec.Encode(new ControlRequest("2", "cove://sys/daemon.stop")), CancellationToken.None);
            Frame resp = (await conn.ReadFrameAsync(CancellationToken.None))!.Value;
            ControlResponse r = ControlCodec.DecodeResponse(resp.Payload);
            Assert.True(r.Data!.Value.GetProperty("stopping").GetBoolean());
        }
        await h.Run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(h.Run.IsCompleted);
    }
}
