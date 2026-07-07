using System.Text.Json;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class HeadlessSmokeTests
{
    [Fact]
    public async Task Create_Split_Focus_Close_OverSocket_NoGui()
    {
        if (System.OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string roomPaneId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "sleep 30" }, 80, 24, ct);

        JsonElement mp = JsonSerializer.SerializeToElement(
            new LayoutMutateParams("createRoom", NewPaneId: roomPaneId, Name: "smoke"),
            CoveJsonContext.Default.LayoutMutateParams);
        ControlResponse createRoom = await RequestAsync(ctl, "cr", "cove://commands/layout.mutate", mp, ct);
        Assert.True(createRoom.Ok, createRoom.Error?.Message);
        string roomId = createRoom.Data!.Value.GetProperty("roomId")!.GetString()!;

        string splitPaneId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "sleep 30" }, 80, 24, ct);
        JsonElement sp = JsonSerializer.SerializeToElement(
            new LayoutMutateParams("split", RoomId: roomId, TargetPaneId: roomPaneId, NewPaneId: splitPaneId, Orientation: "row"),
            CoveJsonContext.Default.LayoutMutateParams);
        ControlResponse splitResp = await RequestAsync(ctl, "sp", "cove://commands/layout.mutate", sp, ct);
        Assert.True(splitResp.Ok, splitResp.Error?.Message);

        JsonElement fp = JsonSerializer.SerializeToElement(
            new LayoutMutateParams("focus", RoomId: roomId, PaneId: splitPaneId),
            CoveJsonContext.Default.LayoutMutateParams);
        ControlResponse focusResp = await RequestAsync(ctl, "fo", "cove://commands/layout.mutate", fp, ct);
        Assert.True(focusResp.Ok, focusResp.Error?.Message);

        ControlResponse closeResp = await RequestAsync(ctl, "kc", "cove://commands/pane.kill",
            JsonSerializer.SerializeToElement(new PaneRefParams(splitPaneId), CoveJsonContext.Default.PaneRefParams), ct);
        Assert.True(closeResp.Ok, closeResp.Error?.Message);

        JsonElement cp = JsonSerializer.SerializeToElement(
            new LayoutMutateParams("close", RoomId: roomId, PaneId: splitPaneId),
            CoveJsonContext.Default.LayoutMutateParams);
        ControlResponse closeTree = await RequestAsync(ctl, "ct", "cove://commands/layout.mutate", cp, ct);
        Assert.True(closeTree.Ok, closeTree.Error?.Message);

        ControlResponse listResp = await RequestAsync(ctl, "li", "cove://commands/pane.list", null, ct);
        Assert.True(listResp.Ok);
        var panes = listResp.Data!.Value.Deserialize(CoveJsonContext.Default.PaneListResult)!.Panes;
        Assert.DoesNotContain(panes, p => p.PaneId == splitPaneId);
        Assert.Contains(panes, p => p.PaneId == roomPaneId);
    }

    private static async Task<string> SpawnAsync(FrameConnection ctl, string command, string[] args, int cols, int rows, CancellationToken ct)
    {
        JsonElement sp = JsonSerializer.SerializeToElement(
            new SpawnParams(command, args, null, null, cols, rows),
            CoveJsonContext.Default.SpawnParams);
        ControlResponse r = await RequestAsync(ctl, "spawn", "cove://commands/pane.spawn", sp, ct);
        Assert.True(r.Ok, r.Error?.Message);
        return r.Data!.Value.Deserialize(CoveJsonContext.Default.PaneInfo)!.PaneId;
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
