using System.Text.Json;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NookHeaderRouteTests
{
    [Fact]
    public async Task Rename_PersistsInEngine_And_ListReturnsTitle()
    {
        if (System.OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string nookId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "sleep 30" }, ct);

        JsonElement rp = JsonSerializer.SerializeToElement(
            new NookRenameParams(nookId, "my terminal"),
            CoveJsonContext.Default.NookRenameParams);
        ControlResponse renameResp = await RequestAsync(ctl, "rn", "cove://commands/nook.rename", rp, ct);
        Assert.True(renameResp.Ok, renameResp.Error?.Message);

        ControlResponse listResp = await RequestAsync(ctl, "li", "cove://commands/nook.list", null, ct);
        Assert.True(listResp.Ok);
        var nooks = listResp.Data!.Value.Deserialize(CoveJsonContext.Default.NookListResult)!.Nooks;
        var renamed = Assert.Single(nooks, p => p.NookId == nookId);
        Assert.Equal("my terminal", renamed.Title);
    }

    [Fact]
    public async Task Rename_UnknownNook_Fails()
    {
        if (System.OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        JsonElement rp = JsonSerializer.SerializeToElement(
            new NookRenameParams("nonexistent", "x"),
            CoveJsonContext.Default.NookRenameParams);
        ControlResponse resp = await RequestAsync(ctl, "rn", "cove://commands/nook.rename", rp, ct);
        Assert.False(resp.Ok);
        Assert.Equal("not_found", resp.Error?.Code);
    }

    private static async Task<string> SpawnAsync(FrameConnection ctl, string command, string[] args, CancellationToken ct)
    {
        JsonElement sp = JsonSerializer.SerializeToElement(
            new SpawnParams(command, args, null, null, 80, 24),
            CoveJsonContext.Default.SpawnParams);
        ControlResponse r = await RequestAsync(ctl, "spawn", "cove://commands/nook.spawn", sp, ct);
        Assert.True(r.Ok, r.Error?.Message);
        return r.Data!.Value.Deserialize(CoveJsonContext.Default.NookInfo)!.NookId;
    }

    private static async Task<ControlResponse> RequestAsync(FrameConnection ctl, string id, string uri, JsonElement? p, CancellationToken ct)
    {
        await ctl.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest(id, uri, p)), ct);
        while (true)
        {
            Frame f = (await ctl.ReadFrameAsync(ct))!.Value;
            if (f.Header.Type != FrameType.Response)
                continue;
            ControlResponse r = ControlCodec.DecodeResponse(f.Payload);
            if (r.Id == id)
                return r;
        }
    }
}
