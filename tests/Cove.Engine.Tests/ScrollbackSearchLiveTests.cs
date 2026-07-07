using System.Text.Json;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ScrollbackSearchLiveTests
{
    [Fact]
    public async Task Search_Finds_String_EmittedIntoRing_OverSocket()
    {
        if (System.OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string paneId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "printf 'NEEDLE_LINE\\n'; sleep 30" }, ct);

        var deadline = Task.Delay(System.TimeSpan.FromSeconds(30), ct);
        bool found = false;
        while (!deadline.IsCompleted)
        {
            JsonElement sp = JsonSerializer.SerializeToElement(new SearchParams(paneId, "NEEDLE_LINE"), CoveJsonContext.Default.SearchParams);
            ControlResponse r = await RequestAsync(ctl, "s", "cove://commands/pane.search", sp, ct);
            if (r.Ok)
            {
                var matches = r.Data!.Value.Deserialize(CoveJsonContext.Default.SearchResult)!.Matches;
                if (matches.Length > 0)
                {
                    Assert.Contains(matches, m => m.Text.Contains("NEEDLE_LINE"));
                    found = true;
                    break;
                }
            }
            await Task.Delay(100, ct);
        }
        Assert.True(found, "NEEDLE_LINE not found in ring search");
    }

    [Fact]
    public async Task Search_Works_OnPane_NotCurrentlySubscribed()
    {
        if (System.OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string paneId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "printf 'HIDDEN_NEEDLE\\n'; sleep 30" }, ct);

        await Task.Delay(1000, ct);

        JsonElement sp = JsonSerializer.SerializeToElement(new SearchParams(paneId, "HIDDEN_NEEDLE"), CoveJsonContext.Default.SearchParams);
        ControlResponse r = await RequestAsync(ctl, "s", "cove://commands/pane.search", sp, ct);
        Assert.True(r.Ok);
        var matches = r.Data!.Value.Deserialize(CoveJsonContext.Default.SearchResult)!.Matches;
        Assert.NotEmpty(matches);
        Assert.Contains(matches, m => m.Text.Contains("HIDDEN_NEEDLE"));
    }

    [Fact]
    public async Task Search_UnknownPane_ReturnsEmpty()
    {
        if (System.OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        JsonElement sp = JsonSerializer.SerializeToElement(new SearchParams("nonexistent", "x"), CoveJsonContext.Default.SearchParams);
        ControlResponse r = await RequestAsync(ctl, "s", "cove://commands/pane.search", sp, ct);
        Assert.True(r.Ok);
        var matches = r.Data!.Value.Deserialize(CoveJsonContext.Default.SearchResult)!.Matches;
        Assert.Empty(matches);
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
