using System.Text.Json;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ConfigChangedEventLiveTests
{
    [Fact]
    public async Task ConfigSet_EmitsConfigChangedEvent()
    {
        if (System.OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("gui");

        JsonElement sp = JsonSerializer.SerializeToElement(new ConfigSetParams("test.key", "value123"), CoveJsonContext.Default.ConfigSetParams);
        await ctl.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("set1", "cove://commands/config.set", sp)), ct);

        string? eventChannel = null;
        string? eventKey = null;
        bool gotResponse = false;
        var deadline = Task.Delay(System.TimeSpan.FromSeconds(10), ct);
        while (!deadline.IsCompleted && (!gotResponse || eventChannel is null))
        {
            Frame f = (await ctl.ReadFrameAsync(ct))!.Value;
            if (f.Header.Type == FrameType.Event)
            {
                var evt = ControlCodec.DecodeEvent(f.Payload);
                if (evt.Channel == "config.changed")
                {
                    eventChannel = evt.Channel;
                    eventKey = evt.Payload.Deserialize(CoveJsonContext.Default.ConfigChangedEvent)?.Key;
                }
            }
            else if (f.Header.Type == FrameType.Response)
            {
                var r = ControlCodec.DecodeResponse(f.Payload);
                if (r.Id == "set1")
                    gotResponse = true;
            }
        }
        Assert.True(gotResponse, "config.set response not received");
        Assert.Equal("config.changed", eventChannel);
        Assert.Equal("test.key", eventKey);
    }
}
