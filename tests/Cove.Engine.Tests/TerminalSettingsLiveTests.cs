using System.Text.Json;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class TerminalSettingsLiveTests
{
    [LiveFact(TestOperatingSystem.Unix)]
    public async Task ConfigSet_PersistsTerminalFontFamily_AndConfigGet_RetrievesIt()
    {
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        JsonElement sp = JsonSerializer.SerializeToElement(new ConfigSetParams("terminal.fontFamily", "JetBrains Mono"), CoveJsonContext.Default.ConfigSetParams);
        ControlResponse setResp = await RequestAsync(ctl, "set", "cove://commands/config.set", sp, ct);
        Assert.True(setResp.Ok, setResp.Error?.Message);

        JsonElement gp = JsonSerializer.SerializeToElement(new ConfigGetParams("terminal.fontFamily"), CoveJsonContext.Default.ConfigGetParams);
        ControlResponse getResp = await RequestAsync(ctl, "get", "cove://commands/config.get", gp, ct);
        Assert.True(getResp.Ok, getResp.Error?.Message);
        var result = getResp.Data!.Value.Deserialize(CoveJsonContext.Default.ConfigGetResult)!;
        Assert.Equal("JetBrains Mono", result.Value);
    }

    [LiveFact(TestOperatingSystem.Unix)]
    public async Task ConfigGet_MissingKey_ReturnsNotFound()
    {
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        JsonElement gp = JsonSerializer.SerializeToElement(new ConfigGetParams("terminal.nonexistent"), CoveJsonContext.Default.ConfigGetParams);
        ControlResponse getResp = await RequestAsync(ctl, "get", "cove://commands/config.get", gp, ct);
        Assert.False(getResp.Ok);
        Assert.Equal("not_found", getResp.Error?.Code);
    }

    [LiveFact(TestOperatingSystem.Unix)]
    public async Task ConfigSet_PersistsAcrossDaemonRestart()
    {
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using (FrameConnection setConn = await h.ConnectAsync("cli"))
        {
            JsonElement sp = JsonSerializer.SerializeToElement(new ConfigSetParams("terminal.fontFamily", "FiraCode Nerd Font"), CoveJsonContext.Default.ConfigSetParams);
            ControlResponse setResp = await RequestAsync(setConn, "set", "cove://commands/config.set", sp, ct);
            Assert.True(setResp.Ok, setResp.Error?.Message);
        }

        await h.RestartAsync();

        await using FrameConnection getConn = await h.ConnectAsync("cli");
        JsonElement gp = JsonSerializer.SerializeToElement(new ConfigGetParams("terminal.fontFamily"), CoveJsonContext.Default.ConfigGetParams);
        ControlResponse getResp = await RequestAsync(getConn, "get", "cove://commands/config.get", gp, ct);
        Assert.True(getResp.Ok, getResp.Error?.Message);
        var result = getResp.Data!.Value.Deserialize(CoveJsonContext.Default.ConfigGetResult)!;
        Assert.Equal("FiraCode Nerd Font", result.Value);
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
