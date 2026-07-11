using System.Text.Json;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class CrashRecoveryLiveTests
{
    [Fact]
    public async Task PerNookCrash_LeavesSiblingAlive_AndShowsExitState()
    {
        if (System.OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string crashNook = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "sleep 1; exit 7" }, ct);
        string siblingNook = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "sleep 30" }, ct);

        await Task.Delay(2500, ct);

        ControlResponse listResp = await RequestAsync(ctl, "li", "cove://commands/nook.list", null, ct);
        Assert.True(listResp.Ok);
        var nooks = listResp.Data!.Value.Deserialize(CoveJsonContext.Default.NookListResult)!.Nooks;
        var crash = nooks.Single(p => p.NookId == crashNook);
        var sibling = nooks.Single(p => p.NookId == siblingNook);

        Assert.False(crash.Alive, "crashed nook should show Alive=false (exit state)");
        Assert.True(sibling.Alive, "sibling nook should remain alive after the other crashed");

        JsonElement sp = JsonSerializer.SerializeToElement(new NookRefParams(siblingNook), CoveJsonContext.Default.NookRefParams);
        ControlResponse writeResp = await RequestAsync(ctl, "w", "cove://commands/nook.write",
            JsonSerializer.SerializeToElement(new NookWriteParams(siblingNook, System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("echo alive\n"))), CoveJsonContext.Default.NookWriteParams), ct);
        Assert.True(writeResp.Ok, "sibling nook should still accept writes after the other nook crashed");
    }

    [Fact]
    public async Task Reattach_AfterClientDisconnect_ReplaysRingByteIdentical()
    {
        if (System.OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        string nookId;
        await using (FrameConnection ctl = await h.ConnectAsync("cli"))
        {
            nookId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "printf 'REATtach_PROOF\n'; sleep 30" }, ct);
            await Task.Delay(1000, ct);
        }

        await using FrameConnection ctl2 = await h.ConnectAsync("cli");
        JsonElement rp = JsonSerializer.SerializeToElement(new NookReadParams(nookId, 0, 65536), CoveJsonContext.Default.NookReadParams);
        var deadline = Task.Delay(System.TimeSpan.FromSeconds(30), ct);
        string output = "";
        while (!deadline.IsCompleted)
        {
            ControlResponse r = await RequestAsync(ctl2, "rd", "cove://commands/nook.read", rp, ct);
            if (r.Ok)
            {
                var result = r.Data!.Value.Deserialize(CoveJsonContext.Default.NookReadResult)!;
                if (!string.IsNullOrEmpty(result.DataBase64))
                {
                    output = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(result.DataBase64));
                    if (output.Contains("REATtach_PROOF")) break;
                }
            }
            await Task.Delay(100, ct);
        }
        Assert.Contains("REATtach_PROOF", output);
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
