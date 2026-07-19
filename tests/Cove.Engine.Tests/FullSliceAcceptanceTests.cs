using System.Text.Json;
using Cove.Engine;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class FullSliceAcceptanceTests
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

    private static (Cove.Tasks.Dispatch.DispatchSaga dispatch, Cove.Tasks.Dispatch.ResumeSaga resume) CreateFakeSagas(Cove.Tasks.TaskService svc)
    {
        var resolver = new AcceptanceFakeProfileResolver();
        var worktree = new AcceptanceFakeWorktreeService();
        var nookHost = new AcceptanceFakeNookHost();
        var shores = new AcceptanceFakeShoreService();
        var launcher = new AcceptanceFakeAgentLauncher();
        var resumeLauncher = new AcceptanceFakeResumeLauncher();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        var dispatch = new Cove.Tasks.Dispatch.DispatchSaga(svc, resolver, worktree, nookHost, shores, launcher, logger);
        var resume = new Cove.Tasks.Dispatch.ResumeSaga(svc, resolver, nookHost, resumeLauncher, logger);
        return (dispatch, resume);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task FullSlice_Launch_SetInReview_SetDone_OverSocket()
    {
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        var svc = new Cove.Tasks.TaskService(h.DataDir, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        await svc.StartAsync();
        var (dispatchSaga, resumeSaga) = CreateFakeSagas(svc);
        h.SetSagas(dispatchSaga, resumeSaga);
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var createResp = await SendAsync(ctl, "c", "cove://commands/task.create", P("""{"title":"acceptance card","bayId":"ws1","source":"user:test","description":"full slice test"}"""), ct);
        Assert.True(createResp.Ok, createResp.Error?.Code);
        var cardId = createResp.Data!.Value.GetProperty("id").GetString()!;
        Assert.True(createResp.Data!.Value.GetProperty("taskNumber").GetInt32() > 0);

        await SendAsync(ctl, "lc", "cove://commands/task.launch-config.set", P($"{{\"cardId\":\"{cardId}\",\"inProgressStatusId\":\"in-progress\",\"reviewStatusId\":\"in-review\",\"completionStatusId\":\"done\"}}"), ct);

        var launchResp = await SendAsync(ctl, "l", "cove://commands/task.launch", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.True(launchResp.Ok, launchResp.Error?.Code);
        Assert.True(launchResp.Data!.Value.GetProperty("success").GetBoolean(), launchResp.Data!.Value.GetRawText());
        var runId = launchResp.Data!.Value.GetProperty("runId").GetString()!;

        var getAfterLaunch = await SendAsync(ctl, "g1", "cove://commands/task.get", P($"{{\"id\":\"{cardId}\"}}"), ct);
        Assert.Equal("in-progress", getAfterLaunch.Data!.Value.GetProperty("statusId").GetString());

        var reviewResp = await SendAsync(ctl, "r", "cove://commands/task.set-in-review", P($"{{\"runId\":\"{runId}\"}}"), ct);
        Assert.True(reviewResp.Ok, reviewResp.Error?.Code);
        Assert.Equal("in-review", reviewResp.Data!.Value.GetProperty("statusId").GetString());

        var doneResp = await SendAsync(ctl, "d", "cove://commands/task.set-done", P($"{{\"runId\":\"{runId}\"}}"), ct);
        Assert.True(doneResp.Ok, doneResp.Error?.Code);
        Assert.Equal("done", doneResp.Data!.Value.GetProperty("statusId").GetString());

        var runAfterDone = await SendAsync(ctl, "rs", "cove://commands/run.show", P($"{{\"id\":\"{runId}\"}}"), ct);
        Assert.Equal("completed", runAfterDone.Data!.Value.GetProperty("state").GetString());

        var listResp = await SendAsync(ctl, "li", "cove://commands/task.list", P("""{"bayId":"ws1"}"""), ct);
        Assert.True(listResp.Ok);
        Assert.True(listResp.Data!.Value.GetProperty("cards").GetArrayLength() >= 1);

        var exportPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-acceptance-" + System.Guid.NewGuid().ToString("N") + ".db");
        var exportResp = await SendAsync(ctl, "e", "cove://commands/task-board.export", P($"{{\"exportPath\":\"{exportPath}\",\"bayCount\":1}}"), ct);
        Assert.True(exportResp.Ok, exportResp.Error?.Code);
        Assert.True(System.IO.File.Exists(exportPath));
        Cove.Testing.TestFile.Delete(exportPath);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task ScheduleLifecycle_Set_Pause_Resume_SkipNext_Stop_OverSocket()
    {
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var createResp = await SendAsync(ctl, "c", "cove://commands/task.create", P("""{"title":"sched lifecycle","bayId":"ws1","source":"user:test"}"""), ct);
        var cardId = createResp.Data!.Value.GetProperty("id").GetString()!;

        var setResp = await SendAsync(ctl, "s", "cove://commands/task.repeat.set", P($"{{\"cardId\":\"{cardId}\",\"triggerKind\":\"cron\",\"cron\":\"0 9 * * *\"}}"), ct);
        Assert.True(setResp.Data!.Value.GetProperty("isValid").GetBoolean());

        await SendAsync(ctl, "p", "cove://commands/task.repeat.pause", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        var paused = await SendAsync(ctl, "gp", "cove://commands/task.repeat.get", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.True(paused.Data!.Value.GetProperty("paused").GetBoolean());

        await SendAsync(ctl, "r", "cove://commands/task.repeat.resume", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        var resumed = await SendAsync(ctl, "gr", "cove://commands/task.repeat.get", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.False(resumed.Data!.Value.GetProperty("paused").GetBoolean());

        await SendAsync(ctl, "sn", "cove://commands/task.repeat.skip-next", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        var skipped = await SendAsync(ctl, "gs", "cove://commands/task.repeat.get", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.True(skipped.Data!.Value.GetProperty("skipNext").GetBoolean());

        await SendAsync(ctl, "st", "cove://commands/task.repeat.stop", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        var afterStop = await SendAsync(ctl, "gas", "cove://commands/task.repeat.get", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.False(afterStop.Ok);
        Assert.Equal("not_found", afterStop.Error!.Code);
    }
}
