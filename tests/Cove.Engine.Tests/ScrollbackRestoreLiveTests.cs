using System.Text;
using System.Text.Json;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ScrollbackRestoreLiveTests
{
    [Fact]
    public async Task ScrollbackSnapshot_SurvivesRestart_AndRestoresIntoRing()
    {
        if (System.OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(90));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        const string marker = "COVE_SCROLLBACK_PROOF";
        JsonElement sp = JsonSerializer.SerializeToElement(
            new SpawnParams("/bin/sh", new[] { "-c", "printf '%s\\n' '" + marker + "'; sleep 30" }, null, null, 80, 24),
            CoveJsonContext.Default.SpawnParams);
        ControlResponse spawnResp = await RequestAsync(ctl, "sp", "cove://commands/pane.spawn", sp, ct);
        Assert.True(spawnResp.Ok, spawnResp.Error?.Message);
        string paneId = spawnResp.Data!.Value.Deserialize(CoveJsonContext.Default.PaneInfo)!.PaneId;

        JsonElement mp = JsonSerializer.SerializeToElement(
            new LayoutMutateParams("createRoom", NewPaneId: paneId, Name: "main"),
            CoveJsonContext.Default.LayoutMutateParams);
        await RequestAsync(ctl, "cr", "cove://commands/layout.mutate", mp, ct);

        await Task.Delay(1500, ct);
        await h.RestartAsync();
        await using FrameConnection ctl2 = await h.ConnectAsync("cli");

        JsonElement rp = JsonSerializer.SerializeToElement(new PaneReadParams(paneId, 0, 65536), CoveJsonContext.Default.PaneReadParams);
        var deadline = Task.Delay(System.TimeSpan.FromSeconds(30), ct);
        string output = "";
        while (!deadline.IsCompleted)
        {
            ControlResponse r = await RequestAsync(ctl2, "rd", "cove://commands/pane.read", rp, ct);
            if (r.Ok)
            {
                var result = r.Data!.Value.Deserialize(CoveJsonContext.Default.PaneReadResult)!;
                if (!string.IsNullOrEmpty(result.DataBase64))
                {
                    output = Encoding.UTF8.GetString(System.Convert.FromBase64String(result.DataBase64));
                    if (output.Contains(marker)) break;
                }
            }
            await Task.Delay(100, ct);
        }
        Assert.Contains(marker, output);
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
