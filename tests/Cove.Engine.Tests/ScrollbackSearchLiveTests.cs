using System.Text.Json;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class ScrollbackSearchLiveTests
{
    [LiveFact(TestOperatingSystem.Unix)]
    public async Task Search_Finds_String_EmittedIntoRing_OverSocket()
    {
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string nookId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "printf 'NEEDLE_LINE\\n'; sleep 30" }, ct);

        var deadline = Task.Delay(System.TimeSpan.FromSeconds(30), ct);
        bool found = false;
        while (!deadline.IsCompleted)
        {
            JsonElement sp = JsonSerializer.SerializeToElement(new SearchParams(nookId, "NEEDLE_LINE"), CoveJsonContext.Default.SearchParams);
            ControlResponse r = await RequestAsync(ctl, "s", "cove://commands/nook.search", sp, ct);
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

    [LiveFact(TestOperatingSystem.Unix)]
    public async Task Search_Works_OnNook_NotCurrentlySubscribed()
    {
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string nookId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "printf 'HIDDEN_NEEDLE\\n'; sleep 30" }, ct);

        JsonElement sp = JsonSerializer.SerializeToElement(new SearchParams(nookId, "HIDDEN_NEEDLE"), CoveJsonContext.Default.SearchParams);
        ControlResponse? r = null;
        await AsyncTest.EventuallyAsync(async () =>
        {
            r = await RequestAsync(ctl, "s", "cove://commands/nook.search", sp, ct);
            return r.Ok && r.Data!.Value.Deserialize(CoveJsonContext.Default.SearchResult)!.Matches.Any();
        }, TimeSpan.FromSeconds(10), "hidden scrollback marker was not searchable", ct);
        Assert.True(r!.Ok);
        var matches = r.Data!.Value.Deserialize(CoveJsonContext.Default.SearchResult)!.Matches;
        Assert.NotEmpty(matches);
        Assert.Contains(matches, m => m.Text.Contains("HIDDEN_NEEDLE"));
    }

    [LiveFact(TestOperatingSystem.Unix)]
    public async Task Search_UnknownNook_ReturnsEmpty()
    {
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        JsonElement sp = JsonSerializer.SerializeToElement(new SearchParams("nonexistent", "x"), CoveJsonContext.Default.SearchParams);
        ControlResponse r = await RequestAsync(ctl, "s", "cove://commands/nook.search", sp, ct);
        Assert.True(r.Ok);
        var matches = r.Data!.Value.Deserialize(CoveJsonContext.Default.SearchResult)!.Matches;
        Assert.Empty(matches);
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
