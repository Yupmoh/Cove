using System.Text.Json;
using Cove.Engine;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SignalingRouteTests
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
        var resp = await SendAsync(ctl, "c", "cove://commands/task.create", P("""{"title":"sig card","bayId":"ws1","source":"user:test"}"""), ct);
        return resp.Data!.Value.GetProperty("id").GetString()!;
    }


    private static async Task<string> CreateCardWithLaunchConfigAsync(FrameConnection ctl, CancellationToken ct)
    {
        var cardId = await CreateCardAsync(ctl, ct);
        var lcResp = await SendAsync(ctl, "lc", "cove://commands/task.launch-config.set", P($"{{\"cardId\":\"{cardId}\",\"inProgressStatusId\":\"in-progress\",\"reviewStatusId\":\"in-review\",\"completionStatusId\":\"done\"}}"), ct);
        Assert.True(lcResp.Ok, lcResp.Error?.Code);
        Assert.True(lcResp.Data!.Value.GetProperty("isValid").GetBoolean(), $"launch-config validation failed: {lcResp.Data!.Value.GetRawText()}");
        return cardId;
    }

    private static async Task<string> ClaimCardAsync(FrameConnection ctl, string cardId, CancellationToken ct)
    {
        var claimResp = await SendAsync(ctl, "cl", "cove://commands/task.claim", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.True(claimResp.Ok, claimResp.Error?.Code);
        return claimResp.Data!.Value.GetProperty("runId").GetString()!;
    }

    [Fact]
    public async Task SetInReview_MovesCardToRunReviewStatus()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var cardId = await CreateCardWithLaunchConfigAsync(ctl, ct);
        var runId = await ClaimCardAsync(ctl, cardId, ct);

        var reviewResp = await SendAsync(ctl, "r", "cove://commands/task.set-in-review", P($"{{\"runId\":\"{runId}\"}}"), ct);
        Assert.True(reviewResp.Ok, reviewResp.Error?.Code);
        Assert.Equal("in-review", reviewResp.Data!.Value.GetProperty("statusId").GetString());
    }

    [Fact]
    public async Task SetDone_MovesCardAndCompletesRun()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var cardId = await CreateCardWithLaunchConfigAsync(ctl, ct);
        var runId = await ClaimCardAsync(ctl, cardId, ct);

        var doneResp = await SendAsync(ctl, "d", "cove://commands/task.set-done", P($"{{\"runId\":\"{runId}\"}}"), ct);
        Assert.True(doneResp.Ok, doneResp.Error?.Code);
        Assert.Equal("done", doneResp.Data!.Value.GetProperty("statusId").GetString());

        var runResp = await SendAsync(ctl, "rs", "cove://commands/run.show", P($"{{\"id\":\"{runId}\"}}"), ct);
        Assert.Equal("completed", runResp.Data!.Value.GetProperty("state").GetString());
    }

    [Fact]
    public async Task SetInReview_DefaultLaunchedRun_RejectsWithNoReviewStatus()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var cardId = await CreateCardAsync(ctl, ct);
        var runId = await ClaimCardAsync(ctl, cardId, ct);

        var reviewResp = await SendAsync(ctl, "r", "cove://commands/task.set-in-review", P($"{{\"runId\":\"{runId}\"}}"), ct);
        Assert.False(reviewResp.Ok);
        Assert.Equal("no_review_status", reviewResp.Error!.Code);
    }

    [Fact]
    public async Task Claim_MintsRunAndRejectsOverlap()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var cardId = await CreateCardAsync(ctl, ct);

        var claim1 = await SendAsync(ctl, "c1", "cove://commands/task.claim", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.True(claim1.Ok, claim1.Error?.Code);
        Assert.NotNull(claim1.Data!.Value.GetProperty("runId").GetString());

        var claim2 = await SendAsync(ctl, "c2", "cove://commands/task.claim", P($"{{\"cardId\":\"{cardId}\"}}"), ct);
        Assert.False(claim2.Ok);
        Assert.Equal("conflict", claim2.Error!.Code);
    }

    [Fact]
    public async Task SetInReview_PrefixRunId_ResolvesCorrectly()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var cardId = await CreateCardWithLaunchConfigAsync(ctl, ct);
        var runId = await ClaimCardAsync(ctl, cardId, ct);
        var prefix = runId[..8];

        var reviewResp = await SendAsync(ctl, "r", "cove://commands/task.set-in-review", P($"{{\"runId\":\"{prefix}\"}}"), ct);
        Assert.True(reviewResp.Ok, reviewResp.Error?.Code);
        Assert.Equal("in-review", reviewResp.Data!.Value.GetProperty("statusId").GetString());
    }

    [Fact]
    public async Task SetInReview_CoveN_TaskId_ResolvesActiveRun()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var cardId = await CreateCardWithLaunchConfigAsync(ctl, ct);
        await ClaimCardAsync(ctl, cardId, ct);

        var getResp = await SendAsync(ctl, "g", "cove://commands/task.get", P($"{{\"id\":\"{cardId}\"}}"), ct);
        var taskNumber = getResp.Data!.Value.GetProperty("taskNumber").GetInt32();

        var reviewResp = await SendAsync(ctl, "r", "cove://commands/task.set-in-review", P($"{{\"runId\":\"COVE-{taskNumber}\",\"bayId\":\"ws1\"}}"), ct);
        Assert.True(reviewResp.Ok, reviewResp.Error?.Code);
        Assert.Equal("in-review", reviewResp.Data!.Value.GetProperty("statusId").GetString());
    }

    [Fact]
    public async Task SetInReview_CoveN_WithoutBay_RejectsWithClearError()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var cardId = await CreateCardWithLaunchConfigAsync(ctl, ct);
        await ClaimCardAsync(ctl, cardId, ct);

        var getResp = await SendAsync(ctl, "g", "cove://commands/task.get", P($"{{\"id\":\"{cardId}\"}}"), ct);
        var taskNumber = getResp.Data!.Value.GetProperty("taskNumber").GetInt32();

        var reviewResp = await SendAsync(ctl, "r", "cove://commands/task.set-in-review", P($"{{\"runId\":\"COVE-{taskNumber}\"}}"), ct);
        Assert.False(reviewResp.Ok);
        Assert.Equal("not_found", reviewResp.Error!.Code);
        Assert.Contains("bay", reviewResp.Error!.Message);
    }
}
