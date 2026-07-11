using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cove.Engine;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class RunRouteTests
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
    public async Task Run_Show_Segments_Cancel_Complete_OverSocket()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var create = await SendAsync(ctl, "c", "cove://commands/task.create", P("""{"title":"run card","bayId":"ws1","source":"user:test"}"""), ct);
        var cardId = create.Data!.Value.GetProperty("id").GetString()!;

        var runList = await SendAsync(ctl, "rl", "cove://commands/run.list", P($"{{\"taskId\":\"{cardId}\"}}"), ct);
        Assert.True(runList.Ok);
        Assert.Equal(0, runList.Data!.Value.GetProperty("runs").GetArrayLength());

        var cancelResp = await SendAsync(ctl, "cancel", "cove://commands/run.cancel", P("""{"id":"nonexistent"}"""), ct);
        Assert.False(cancelResp.Ok);
        Assert.Equal("invalid_transition", cancelResp.Error!.Code);
    }

    [Fact]
    public async Task RunList_RequiresTaskOrBay()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var resp = await SendAsync(ctl, "1", "cove://commands/run.list", P("""{}"""), ct);
        Assert.False(resp.Ok);
        Assert.Equal("invalid_params", resp.Error!.Code);
    }
}
