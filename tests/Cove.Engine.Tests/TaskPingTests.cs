using System.Text.Json;
using Cove.Engine;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class TaskPingTests
{
    private static JsonElement Params(string json)
        => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public async Task Ping_RoundTripsThroughRouter_WithCamelCaseEcho()
    {
        var request = new ControlRequest("1", "cove://commands/task.ping", Params("""{"echo":"hello"}"""));
        var response = await EngineCommandRouter.RouteAsync(request);
        Assert.NotNull(response);
        Assert.True(response!.Ok);
        var raw = response.Data!.Value.GetRawText();
        Assert.Contains("\"echo\":\"hello\"", raw);
        Assert.Contains("\"status\":\"pong\"", raw);
    }

    [Fact]
    public async Task Ping_WithoutParams_ReturnsInvalidParams()
    {
        var request = new ControlRequest("1", "cove://commands/task.ping");
        var response = await EngineCommandRouter.RouteAsync(request);
        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("invalid_params", response.Error!.Code);
    }

    [Fact]
    public void Ping_RouteIsRegisteredInCatalogue()
    {
        var entries = EngineCommandCatalogue.Entries;
        Assert.Contains(entries, e => e.Command == "cove://commands/task.ping");
    }

    [Fact]
    public async Task Ping_RoundTripsOverSocket_FromClient()
    {
        if (System.OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        JsonElement p = Params("""{"echo":"hello"}""");
        ControlResponse resp = await RequestAsync(ctl, "ping", "cove://commands/task.ping", p, ct);
        Assert.True(resp.Ok, resp.Error?.Message);
        var raw = resp.Data!.Value.GetRawText();
        Assert.Contains("\"echo\":\"hello\"", raw);
        Assert.Contains("\"status\":\"pong\"", raw);
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
