using System.Text.Json;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class CwdInheritanceLiveTests
{
    [LiveFact(TestOperatingSystem.Unix)]
    public async Task Split_Inherits_LiveOsc7Cwd_OverSocket()
    {
        Assert.True(System.IO.File.Exists("/bin/zsh") || System.IO.File.Exists("/usr/bin/zsh")
            || System.IO.File.Exists("/bin/bash") || System.IO.File.Exists("/usr/bin/bash"),
            "Requires zsh or bash");

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

        JsonElement wp = JsonSerializer.SerializeToElement(
            new NookWriteParams(nookA, System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("cd /tmp\n"))),
            CoveJsonContext.Default.NookWriteParams);
        Assert.True((await RequestAsync(ctl, "wA", "cove://commands/nook.write", wp, ct)).Ok);

        JsonElement ssp = JsonSerializer.SerializeToElement(new NookRefParams(nookA), CoveJsonContext.Default.NookRefParams);
        await AsyncTest.EventuallyAsync(async () =>
        {
            ControlResponse st = await RequestAsync(ctl, "stA", "cove://commands/session.state", ssp, ct);
            return st.Ok && st.Data!.Value.Deserialize(CoveJsonContext.Default.SessionStateResult)!.Cwd?.EndsWith("/tmp") == true;
        }, TimeSpan.FromSeconds(30), "nook A did not report /tmp", ct);

        JsonElement spB = JsonSerializer.SerializeToElement(
            new SpawnParams(shell, System.Array.Empty<string>(), null, null, 80, 24, InheritCwdFrom: nookA),
            CoveJsonContext.Default.SpawnParams);
        ControlResponse respB = await RequestAsync(ctl, "sB", "cove://commands/nook.spawn", spB, ct);
        Assert.True(respB.Ok, respB.Error?.Message);
        string nookB = respB.Data!.Value.Deserialize(CoveJsonContext.Default.NookInfo)!.NookId;

        JsonElement sspB = JsonSerializer.SerializeToElement(new NookRefParams(nookB), CoveJsonContext.Default.NookRefParams);
        SessionStateResult? stateB = null;
        await AsyncTest.EventuallyAsync(async () =>
        {
            ControlResponse stB = await RequestAsync(ctl, "stB", "cove://commands/session.state", sspB, ct);
            stateB = stB.Ok ? stB.Data!.Value.Deserialize(CoveJsonContext.Default.SessionStateResult) : null;
            return stateB?.Cwd?.EndsWith("/tmp") == true;
        }, TimeSpan.FromSeconds(30), "nook B did not inherit /tmp", ct);
        Assert.True(stateB!.Cwd?.EndsWith("/tmp"), $"expected nook B cwd to inherit /tmp from nook A, got {stateB.Cwd}");
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
