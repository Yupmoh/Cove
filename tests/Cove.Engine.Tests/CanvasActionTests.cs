using System.Text.Json;
using Cove.Engine;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class CanvasActionTests
{
    private static JsonElement P(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static async Task<ControlResponse> SendAsync(FrameConnection ctl, string id, string uri, JsonElement? p, CancellationToken ct)
    {
        await ctl.WriteFrameAsync(FrameType.Request, 0, ControlCodec.Encode(new ControlRequest(id, uri, p)), ct);
        while (true)
        {
            Frame f = (await ctl.ReadFrameAsync(ct))!.Value;
            if (f.Header.Type != FrameType.Response) continue;
            ControlResponse r = ControlCodec.DecodeResponse(f.Payload);
            if (r.Id == id) return r;
        }
    }

    [Fact]
    public async Task CoveCommandAction_ReturnsResolvedUri()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var resp = await SendAsync(ctl, "a1", "cove://commands/canvas.action", P("""{"action":"cove_command","uri":"cove://commands/note.list","actionId":"el-123","payload":"{}","state":{}}"""), ct);
        Assert.True(resp.Ok, resp.Error?.Code);
        Assert.True(resp.Data!.Value.GetProperty("dispatched").GetBoolean());
        Assert.Equal("cove://commands/note.list", resp.Data!.Value.GetProperty("resolvedUri").GetString());
    }

    [Fact]
    public async Task SendToAgentAction_DispatchesPayloadToTargetPane()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var spawnResp = await SendAsync(ctl, "sp", "cove://commands/pane.spawn", P("""{"command":"/bin/cat","args":[],"cwd":"/tmp","cols":80,"rows":24}"""), ct);
        Assert.True(spawnResp.Ok, spawnResp.Error?.Code);
        var paneId = spawnResp.Data!.Value.GetProperty("paneId").GetString()!;

        var resp = await SendAsync(ctl, "a2", "cove://commands/canvas.action", P($"{{\"action\":\"send_to_agent\",\"targetPane\":\"{paneId}\",\"actionId\":\"el-456\",\"payload\":\"hello agent\",\"state\":{{}}}}"), ct);
        Assert.True(resp.Ok, resp.Error?.Code);
        Assert.True(resp.Data!.Value.GetProperty("dispatched").GetBoolean());
        Assert.Equal(paneId, resp.Data!.Value.GetProperty("targetPaneId").GetString());
    }

    [Fact]
    public async Task SendToAgentAction_NonexistentPane_ReturnsNotFoundError()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var resp = await SendAsync(ctl, "a2", "cove://commands/canvas.action", P("""{"action":"send_to_agent","targetPane":"nonexistent-pane","actionId":"el-456","payload":"hello","state":{}}"""), ct);
        Assert.False(resp.Ok);
        Assert.Equal("not_found", resp.Error!.Code);
    }

    [Fact]
    public async Task UnknownAction_ReturnsInvalidActionError()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var resp = await SendAsync(ctl, "a3", "cove://commands/canvas.action", P("""{"action":"unknown","actionId":"el-789","payload":null,"state":{}}"""), ct);
        Assert.False(resp.Ok);
        Assert.Equal("invalid_action", resp.Error!.Code);
    }

    [Fact]
    public async Task CoveCommandWithoutUri_ReturnsInvalidParamsError()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var resp = await SendAsync(ctl, "a4", "cove://commands/canvas.action", P("""{"action":"cove_command","uri":null,"actionId":"el-000","payload":null,"state":{}}"""), ct);
        Assert.False(resp.Ok);
        Assert.Equal("invalid_params", resp.Error!.Code);
    }
}
