using System.Text.Json;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class HeadlessSmokeTests
{
    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task Create_Split_Focus_Close_OverSocket_NoGui()
    {
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string shoreNookId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "sleep 30" }, 80, 24, ct);

        JsonElement mp = JsonSerializer.SerializeToElement(
            new LayoutMutateParams("createShore", NewNookId: shoreNookId, Name: "smoke"),
            CoveJsonContext.Default.LayoutMutateParams);
        ControlResponse createShore = await RequestAsync(ctl, "cr", "cove://commands/layout.mutate", mp, ct);
        Assert.True(createShore.Ok, createShore.Error?.Message);
        string shoreId = createShore.Data!.Value.GetProperty("shoreId")!.GetString()!;

        string splitNookId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "sleep 30" }, 80, 24, ct);
        JsonElement sp = JsonSerializer.SerializeToElement(
            new LayoutMutateParams("split", ShoreId: shoreId, TargetNookId: shoreNookId, NewNookId: splitNookId, Orientation: "row"),
            CoveJsonContext.Default.LayoutMutateParams);
        ControlResponse splitResp = await RequestAsync(ctl, "sp", "cove://commands/layout.mutate", sp, ct);
        Assert.True(splitResp.Ok, splitResp.Error?.Message);

        JsonElement fp = JsonSerializer.SerializeToElement(
            new LayoutMutateParams("focus", ShoreId: shoreId, NookId: splitNookId),
            CoveJsonContext.Default.LayoutMutateParams);
        ControlResponse focusResp = await RequestAsync(ctl, "fo", "cove://commands/layout.mutate", fp, ct);
        Assert.True(focusResp.Ok, focusResp.Error?.Message);

        ControlResponse closeResp = await RequestAsync(ctl, "kc", "cove://commands/nook.kill",
            JsonSerializer.SerializeToElement(new NookRefParams(splitNookId), CoveJsonContext.Default.NookRefParams), ct);
        Assert.True(closeResp.Ok, closeResp.Error?.Message);

        JsonElement cp = JsonSerializer.SerializeToElement(
            new LayoutMutateParams("close", ShoreId: shoreId, NookId: splitNookId),
            CoveJsonContext.Default.LayoutMutateParams);
        ControlResponse closeTree = await RequestAsync(ctl, "ct", "cove://commands/layout.mutate", cp, ct);
        Assert.True(closeTree.Ok, closeTree.Error?.Message);

        ControlResponse listResp = await RequestAsync(ctl, "li", "cove://commands/nook.list", null, ct);
        Assert.True(listResp.Ok);
        var nooks = listResp.Data!.Value.Deserialize(CoveJsonContext.Default.NookListResult)!.Nooks;
        Assert.DoesNotContain(nooks, p => p.NookId == splitNookId);
        Assert.Contains(nooks, p => p.NookId == shoreNookId);
    }

    private static async Task<string> SpawnAsync(FrameConnection ctl, string command, string[] args, int cols, int rows, CancellationToken ct)
    {
        JsonElement sp = JsonSerializer.SerializeToElement(
            new SpawnParams(command, args, null, null, cols, rows),
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
