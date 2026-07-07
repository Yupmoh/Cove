using System.Text.Json;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class LosslessFloodLiveTests
{
    [Fact]
    public async Task YesFlood_NeverDropsByte_OverRingRotation()
    {
        if (System.OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(90));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string paneId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "yes FLOOD_MARK_LINE; sleep 30" }, ct);
        await Task.Delay(3000, ct);

        ControlResponse subResp = await RequestAsync(ctl, "sub", "cove://commands/pane.subscribe",
            JsonSerializer.SerializeToElement(new PaneRefParams(paneId), CoveJsonContext.Default.PaneRefParams), ct);
        Assert.True(subResp.Ok, subResp.Error?.Message);

        var seen = new System.Collections.Generic.HashSet<string>();
        var deadline = Task.Delay(System.TimeSpan.FromSeconds(15), ct);
        int totalLines = 0;
        while (!deadline.IsCompleted)
        {
            try
            {
                Frame f = (await ctl.ReadFrameAsync(ct))!.Value;
                if (f.Header.Type != FrameType.StreamData)
                    continue;
                string text = System.Text.Encoding.UTF8.GetString(f.Payload);
                totalLines += text.Split('\n').Length - 1;
                seen.Add(text.Trim());
            }
            catch
            {
                break;
            }
        }
        Assert.True(seen.Count >= 1, "expected at least the yes-flood marker line");
        Assert.Contains("FLOOD_MARK_LINE", seen.First());
        Assert.True(totalLines > 1000, $"expected substantial flood output, got {totalLines} lines");
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
