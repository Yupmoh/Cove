using System.Text.Json;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class Osc7LiveTests
{
    [Fact]
    public async Task Cd_UpdatesNookCwd_ViaOsc7_OverSocket()
    {
        if (System.OperatingSystem.IsWindows())
            return;
        if (!System.IO.File.Exists("/bin/zsh") && !System.IO.File.Exists("/usr/bin/zsh") && !System.IO.File.Exists("/bin/bash") && !System.IO.File.Exists("/usr/bin/bash"))
            return;

        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string shell = System.IO.File.Exists("/bin/zsh") ? "/bin/zsh" : (System.IO.File.Exists("/usr/bin/zsh") ? "/usr/bin/zsh" : "/bin/bash");
        JsonElement sp = JsonSerializer.SerializeToElement(
            new SpawnParams(shell, System.Array.Empty<string>(), null, null, 80, 24),
            CoveJsonContext.Default.SpawnParams);
        ControlResponse spawnResp = await RequestAsync(ctl, "spawn", "cove://commands/nook.spawn", sp, ct);
        Assert.True(spawnResp.Ok, spawnResp.Error?.Message);
        string nookId = spawnResp.Data!.Value.Deserialize(CoveJsonContext.Default.NookInfo)!.NookId;

        await Task.Delay(1000, ct);
        JsonElement wp = JsonSerializer.SerializeToElement(
            new NookWriteParams(nookId, System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("cd /tmp\n"))),
            CoveJsonContext.Default.NookWriteParams);
        ControlResponse writeResp = await RequestAsync(ctl, "write", "cove://commands/nook.write", wp, ct);
        Assert.True(writeResp.Ok, writeResp.Error?.Message);

        JsonElement ssp = JsonSerializer.SerializeToElement(new NookRefParams(nookId), CoveJsonContext.Default.NookRefParams);
        string? cwd = null;
        var deadline = Task.Delay(System.TimeSpan.FromSeconds(30), ct);
        while (!deadline.IsCompleted)
        {
            ControlResponse st = await RequestAsync(ctl, "st", "cove://commands/session.state", ssp, ct);
            if (st.Ok)
            {
                var res = st.Data!.Value.Deserialize(CoveJsonContext.Default.SessionStateResult)!;
                if (!string.IsNullOrEmpty(res.Cwd))
                {
                    cwd = res.Cwd;
                    if (cwd.EndsWith("/tmp"))
                        break;
                }
            }
            await Task.Delay(100, ct);
        }

        Assert.NotNull(cwd);
        Assert.EndsWith("/tmp", cwd);
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
