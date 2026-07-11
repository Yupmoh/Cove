using System.Text.Json;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class CwdInheritanceLiveTests
{
    [Fact]
    public async Task Split_Inherits_LiveOsc7Cwd_OverSocket()
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
        JsonElement spA = JsonSerializer.SerializeToElement(
            new SpawnParams(shell, System.Array.Empty<string>(), null, null, 80, 24),
            CoveJsonContext.Default.SpawnParams);
        ControlResponse respA = await RequestAsync(ctl, "sA", "cove://commands/nook.spawn", spA, ct);
        Assert.True(respA.Ok, respA.Error?.Message);
        string nookA = respA.Data!.Value.Deserialize(CoveJsonContext.Default.NookInfo)!.NookId;

        await Task.Delay(1000, ct);
        JsonElement wp = JsonSerializer.SerializeToElement(
            new NookWriteParams(nookA, System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("cd /tmp\n"))),
            CoveJsonContext.Default.NookWriteParams);
        Assert.True((await RequestAsync(ctl, "wA", "cove://commands/nook.write", wp, ct)).Ok);

        JsonElement ssp = JsonSerializer.SerializeToElement(new NookRefParams(nookA), CoveJsonContext.Default.NookRefParams);
        var deadline = Task.Delay(System.TimeSpan.FromSeconds(30), ct);
        while (!deadline.IsCompleted)
        {
            ControlResponse st = await RequestAsync(ctl, "stA", "cove://commands/session.state", ssp, ct);
            if (st.Ok && st.Data!.Value.Deserialize(CoveJsonContext.Default.SessionStateResult)!.Cwd?.EndsWith("/tmp") == true)
                break;
            await Task.Delay(100, ct);
        }

        JsonElement spB = JsonSerializer.SerializeToElement(
            new SpawnParams(shell, System.Array.Empty<string>(), null, null, 80, 24, InheritCwdFrom: nookA),
            CoveJsonContext.Default.SpawnParams);
        ControlResponse respB = await RequestAsync(ctl, "sB", "cove://commands/nook.spawn", spB, ct);
        Assert.True(respB.Ok, respB.Error?.Message);
        string nookB = respB.Data!.Value.Deserialize(CoveJsonContext.Default.NookInfo)!.NookId;

        await Task.Delay(500, ct);
        JsonElement sspB = JsonSerializer.SerializeToElement(new NookRefParams(nookB), CoveJsonContext.Default.NookRefParams);
        ControlResponse stB = await RequestAsync(ctl, "stB", "cove://commands/session.state", sspB, ct);
        Assert.True(stB.Ok);
        var stateB = stB.Data!.Value.Deserialize(CoveJsonContext.Default.SessionStateResult)!;
        Assert.True(stateB.Cwd?.EndsWith("/tmp"), $"expected nook B cwd to inherit /tmp from nook A, got {stateB.Cwd}");
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
