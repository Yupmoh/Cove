using System.Buffers.Binary;
using System.Text.Json;
using Cove.Engine.Bays;
using Cove.Protocol;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class PtyStreamBroadcastIsolationTests
{
    private static async Task<ControlResponse> RequestAsync(FrameConnection conn, string id, string uri, JsonElement? p, CancellationToken ct)
    {
        await conn.WriteFrameAsync(FrameType.Request, 0, ControlCodec.Encode(new ControlRequest(id, uri, p)), ct);
        while (true)
        {
            var frame = (await conn.ReadFrameAsync(ct))!.Value;
            if (frame.Header.Type != FrameType.Response)
                continue;
            var response = ControlCodec.DecodeResponse(frame.Payload);
            if (response.Id == id)
                return response;
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task GuiStream_DoesNotReceiveBroadcastEventsWhileStreamingPtyData()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;
        await using var harness = await DaemonTestHarness.StartAsync();
        await using var control = await harness.ConnectAsync("gui");
        await using var stream = await harness.ConnectAsync("gui-stream");

        var spawnParams = JsonSerializer.SerializeToElement(
            new SpawnParams("/bin/sh", ["-c", "i=0; while [ $i -lt 250 ]; do printf x; i=$((i+1)); done; sleep 2"], null, null, 80, 24),
            CoveJsonContext.Default.SpawnParams);
        var spawned = await RequestAsync(control, "spawn", "cove://commands/nook.spawn", spawnParams, ct);
        Assert.True(spawned.Ok, spawned.Error?.Message);
        var nookId = spawned.Data!.Value.Deserialize(CoveJsonContext.Default.NookInfo)!.NookId;

        var subscribeParams = JsonSerializer.SerializeToElement(new SubscribeParams(nookId, 0), CoveJsonContext.Default.SubscribeParams);
        var subscribed = await RequestAsync(stream, "subscribe", "cove://commands/nook.subscribe", subscribeParams, ct);
        Assert.True(subscribed.Ok, subscribed.Error?.Message);
        var sub = subscribed.Data!.Value.Deserialize(CoveJsonContext.Default.SubscribeResult)!;

        var createParams = JsonSerializer.SerializeToElement(new BayCreateParams("broadcast-proof", "/tmp/broadcast-proof", null), BaysJsonContext.Default.BayCreateParams);
        var broadcastTask = RequestAsync(control, "create", "cove://commands/bay.create", createParams, ct);

        var received = 0;
        while (received < 250)
        {
            var frame = (await stream.ReadFrameAsync(ct))!.Value;
            Assert.NotEqual(FrameType.Event, frame.Header.Type);
            if (frame.Header.Type != FrameType.StreamData || frame.Header.StreamId != sub.StreamId)
                continue;
            Assert.True(frame.Payload.Length >= 8);
            var offset = BinaryPrimitives.ReadUInt64LittleEndian(frame.Payload);
            Assert.Equal((ulong)received, offset);
            received += frame.Payload.Length - 8;
        }

        var create = await broadcastTask;
        Assert.True(create.Ok, create.Error?.Message);
        Assert.Equal(250, received);
    }
}
