using System.Text.Json;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class DeepScrollbackSearchLiveTests
{
    [Fact]
    public async Task Search_FindsMarker_ThousandsOfLinesBack()
    {
        if (System.OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(90));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string paneId = await SpawnAsync(ctl, "/bin/sh",
            new[] { "-c", "for i in $(seq 1 6000); do printf 'line %d\\n' \"$i\"; done; printf 'NEEDLE_IN_HAYSTACK\\n'; sleep 30" }, ct);

        var readyDeadline = Task.Delay(System.TimeSpan.FromSeconds(10), ct);
        while (!readyDeadline.IsCompleted)
        {
            ControlResponse rd = await RequestAsync(ctl, "rd", "cove://commands/pane.read",
                JsonSerializer.SerializeToElement(new PaneReadParams(paneId, 0, 65536), CoveJsonContext.Default.PaneReadParams), ct);
            if (rd.Ok)
            {
                var pr = rd.Data!.Value.Deserialize(CoveJsonContext.Default.PaneReadResult)!;
                if (pr.Head > 100000)
                    break;
            }
            await Task.Delay(200, ct);
        }

        ControlResponse searchResp = await RequestAsync(ctl, "search", "cove://commands/pane.search",
            JsonSerializer.SerializeToElement(new SearchParams(paneId, "NEEDLE_IN_HAYSTACK", false), CoveJsonContext.Default.SearchParams), ct);
        Assert.True(searchResp.Ok, searchResp.Error?.Message);
        var result = searchResp.Data!.Value.Deserialize(CoveJsonContext.Default.SearchResult)!;
        Assert.NotEmpty(result.Matches);
        Assert.Contains(result.Matches, m => m.Text.Contains("NEEDLE_IN_HAYSTACK"));
    }

    private static async Task<string> SpawnAsync(FrameConnection ctl, string command, string[] args, CancellationToken ct)
    {
        JsonElement sp = JsonSerializer.SerializeToElement(
            new SpawnParams(command, args, null, null, 80, 24),
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
