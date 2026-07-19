using System.Text.Json;
using Cove.Engine;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class ResumeRouteTests
{
    private static JsonElement P(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static async Task<ControlResponse> SendAsync(FrameConnection ctl, string id, string uri, JsonElement? p, CancellationToken ct)
    {
        await ctl.WriteFrameAsync(FrameType.Request, 0, ControlCodec.Encode(new ControlRequest(id, uri, p)), ct);
        while (true)
        {
            Frame f = (await ctl.ReadFrameAsync(ct))!.Value;
            if (f.Header.Type != FrameType.Response) continue;
            ControlResponse r = ControlCodec.DecodeResponse(f.Payload);
            if (r.Id == id) return r;
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task RunResume_RequiresId()
    {
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var resp = await SendAsync(ctl, "1", "cove://commands/run.resume", P("""{}"""), ct);
        Assert.False(resp.Ok);
        Assert.Equal("invalid_params", resp.Error!.Code);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task RunResume_WithoutSaga_ReturnsNotReady()
    {
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var resp = await SendAsync(ctl, "1", "cove://commands/run.resume", P("""{"id":"some-run-id"}"""), ct);
        Assert.False(resp.Ok);
        Assert.Equal("not_ready", resp.Error!.Code);
    }
}
