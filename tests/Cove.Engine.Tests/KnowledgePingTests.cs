using System.Text.Json;
using Cove.Engine;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class KnowledgePingTests
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

    [Fact]
    public async Task KnowledgePing_RoundTripsOverSocket_FromClient()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var resp = await SendAsync(ctl, "p", "cove://commands/knowledge.ping", P("""{"echo":"hello"}"""), ct);
        Assert.True(resp.Ok, resp.Error?.Code);
        Assert.Equal("pong", resp.Data!.Value.GetProperty("pong").GetString());
        Assert.Equal("hello", resp.Data!.Value.GetProperty("echo").GetString());
    }

    [Fact]
    public async Task KnowledgePing_NoParams_StillWorks()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var resp = await SendAsync(ctl, "p", "cove://commands/knowledge.ping", null, ct);
        Assert.True(resp.Ok);
        Assert.Equal("pong", resp.Data!.Value.GetProperty("pong").GetString());
    }
}
