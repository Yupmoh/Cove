using System.Text.Json;
using Cove.Engine;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ScheduleRouteTests
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

    private static async Task<string> CreateCardAsync(FrameConnection ctl, CancellationToken ct)
    {
        var resp = await SendAsync(ctl, "c", "cove://commands/task.create", P("""{"title":"sched card","workspaceId":"ws1","source":"user:test"}"""), ct);
        return resp.Data!.Value.GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task RepeatSet_Get_Pause_Resume_SkipNext_Stop_OverSocket()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var cardId = await CreateCardAsync(ctl, ct);

        var setResp = await SendAsync(ctl, "s", "cove://commands/task.repeat.set", P($"{{\"cardId\":\"{cardId}\",\"triggerKind\":\"cron\",\"cron\":\"0 9 * * *\"}}"), ct);
        Assert.True(setResp.Ok, setResp.Error?.Code);
        Assert.True(setResp.Data!.Value.GetProperty("isValid").GetBoolean(), setResp.Data!.Value.GetRawText());
        Assert.NotNull(setResp.Data!.Value.GetProperty("nextFireAt").GetString());

        var getResp = await SendAsync(ctl, "g", "cove://commands/task.repeat.get", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.True(getResp.Ok, getResp.Error?.Code);
        Assert.Equal("cron", getResp.Data!.Value.GetProperty("triggerKind").GetString());
        Assert.Equal("Scheduled", getResp.Data!.Value.GetProperty("mode").GetString());

        var pauseResp = await SendAsync(ctl, "p", "cove://commands/task.repeat.pause", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.True(pauseResp.Ok, pauseResp.Error?.Code);

        var getPaused = await SendAsync(ctl, "gp", "cove://commands/task.repeat.get", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.True(getPaused.Data!.Value.GetProperty("paused").GetBoolean());
        Assert.Equal("Paused", getPaused.Data!.Value.GetProperty("mode").GetString());

        var resumeResp = await SendAsync(ctl, "r", "cove://commands/task.repeat.resume", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.True(resumeResp.Ok, resumeResp.Error?.Code);

        var skipResp = await SendAsync(ctl, "sn", "cove://commands/task.repeat.skip-next", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.True(skipResp.Ok, skipResp.Error?.Code);

        var getSkip = await SendAsync(ctl, "gs", "cove://commands/task.repeat.get", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.True(getSkip.Data!.Value.GetProperty("skipNext").GetBoolean());

        var stopResp = await SendAsync(ctl, "st", "cove://commands/task.repeat.stop", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.True(stopResp.Ok, stopResp.Error?.Code);

        var getAfterStop = await SendAsync(ctl, "gas", "cove://commands/task.repeat.get", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.False(getAfterStop.Ok);
        Assert.Equal("not_found", getAfterStop.Error!.Code);
    }

    [Fact]
    public async Task RepeatSet_BadCron_RejectsWithValidationErrors()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var cardId = await CreateCardAsync(ctl, ct);

        var setResp = await SendAsync(ctl, "s", "cove://commands/task.repeat.set", P($"{{\"cardId\":\"{cardId}\",\"triggerKind\":\"cron\",\"cron\":\"not-valid\"}}"), ct);
        Assert.True(setResp.Ok);
        Assert.False(setResp.Data!.Value.GetProperty("isValid").GetBoolean());
        Assert.True(setResp.Data!.Value.GetProperty("errors").GetArrayLength() > 0);
    }

    [Fact]
    public async Task RepeatSet_Datetime_ComputesNextFireAt()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var cardId = await CreateCardAsync(ctl, ct);

        var setResp = await SendAsync(ctl, "s", "cove://commands/task.repeat.set", P($"{{\"cardId\":\"{cardId}\",\"triggerKind\":\"datetime\",\"at\":\"2026-12-25T10:00:00Z\"}}"), ct);
        Assert.True(setResp.Ok);
        Assert.True(setResp.Data!.Value.GetProperty("isValid").GetBoolean());
        Assert.NotNull(setResp.Data!.Value.GetProperty("nextFireAt").GetString());

        var getResp = await SendAsync(ctl, "g", "cove://commands/task.repeat.get", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.Equal("Repeat", getResp.Data!.Value.GetProperty("mode").GetString());
    }
}
