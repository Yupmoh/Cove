using System.Text.Json;
using Cove.Engine;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class LoopRouteTests
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

    private static async Task<string> CreateCardWithScheduleAsync(FrameConnection ctl, CancellationToken ct)
    {
        var resp = await SendAsync(ctl, "c", "cove://commands/task.create", P("""{"title":"loop card","workspaceId":"ws1","source":"user:test"}"""), ct);
        var cardId = resp.Data!.Value.GetProperty("id").GetString()!;
        await SendAsync(ctl, "s", "cove://commands/task.repeat.set", P($"{{\"cardId\":\"{cardId}\",\"triggerKind\":\"immediate\"}}"), ct);
        return cardId;
    }

    [Fact]
    public async Task RunNow_FiresOffCadenceEvenWithActiveRun()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var cardId = await CreateCardWithScheduleAsync(ctl, ct);
        await SendAsync(ctl, "cl", "cove://commands/task.claim", P($"{{\"cardId\":\"{cardId}\"}}"), ct);

        var runNowResp = await SendAsync(ctl, "rn", "cove://commands/task.run-now", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.True(runNowResp.Ok, runNowResp.Error?.Code);
        Assert.NotNull(runNowResp.Data!.Value.GetProperty("runId").GetString());

        var runs = await SendAsync(ctl, "rl", "cove://commands/run.list", P($"{{\"taskId\":\"{cardId}\"}}"), ct);
        Assert.Equal(2, runs.Data!.Value.GetProperty("runs").GetArrayLength());
    }

    [Fact]
    public async Task RepeatContinue_WritesPendingIntent()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var cardId = await CreateCardWithScheduleAsync(ctl, ct);

        var continueResp = await SendAsync(ctl, "cnt", "cove://commands/task.repeat.continue", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.True(continueResp.Ok, continueResp.Error?.Code);

        var getResp = await SendAsync(ctl, "g", "cove://commands/task.repeat.get", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.True(getResp.Ok, getResp.Error?.Code);
    }

    [Fact]
    public async Task RepeatFinish_WritesPendingIntent()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var cardId = await CreateCardWithScheduleAsync(ctl, ct);

        var finishResp = await SendAsync(ctl, "fn", "cove://commands/task.repeat.finish", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.True(finishResp.Ok, finishResp.Error?.Code);
    }

    [Fact]
    public async Task RepeatContinue_NoSchedule_ReturnsNotFound()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var cardId = (await SendAsync(ctl, "c", "cove://commands/task.create", P("""{"title":"no-sched","workspaceId":"ws1","source":"user:test"}"""), ct)).Data!.Value.GetProperty("id").GetString()!;

        var continueResp = await SendAsync(ctl, "cnt", "cove://commands/task.repeat.continue", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.False(continueResp.Ok);
        Assert.Equal("not_found", continueResp.Error!.Code);
    }
}
