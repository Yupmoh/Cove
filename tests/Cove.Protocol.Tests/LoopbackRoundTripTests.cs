using System.Text;
using System.Text.Json;
using Cove.Platform.Ipc;
using Xunit;

namespace Cove.Protocol.Tests;

public sealed class LoopbackRoundTripTests
{
    [Fact]
    public async Task Loopback_Hello_Ping_InterleavedStreams_Credit()
    {
        string dir = Path.Combine(Path.GetTempPath(), "cove-ipc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string socketPath = Path.Combine(dir, "dev.sock");
        IControlEndpoint endpoint = ControlEndpointFactory.FromSocketPath(socketPath);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        ulong readCreditAck = 0;

        await using IControlListener listener = endpoint.Bind();
        Task server = Task.Run(async () =>
        {
            await using Stream s = await listener.AcceptAsync(cts.Token);
            await using var conn = new FrameConnection(s);

            Frame hello = (await conn.ReadFrameAsync(cts.Token))!.Value;
            ControlRequest helloReq = ControlCodec.DecodeRequest(hello.Payload);
            Assert.Equal("cove://sys/hello", helloReq.Uri);
            JsonElement hr = JsonSerializer.SerializeToElement(
                new HelloResult(1, "0.1.0", 4242, "dev"), CoveJsonContext.Default.HelloResult);
            await conn.WriteFrameAsync(FrameType.Response, 0,
                ControlCodec.Encode(new ControlResponse(helloReq.Id, true, hr)), cts.Token);

            Frame ping = (await conn.ReadFrameAsync(cts.Token))!.Value;
            ControlRequest pingReq = ControlCodec.DecodeRequest(ping.Payload);
            Assert.Equal("cove://sys/ping", pingReq.Uri);
            JsonElement pong = JsonDocument.Parse("{\"pong\":true}").RootElement.Clone();
            await conn.WriteFrameAsync(FrameType.Response, 0,
                ControlCodec.Encode(new ControlResponse(pingReq.Id, true, pong)), cts.Token);

            await conn.WriteFrameAsync(FrameType.StreamData, 1, MakeStreamData(0, "hi\r\n"), cts.Token);
            await conn.WriteFrameAsync(FrameType.StreamData, 2, MakeStreamData(0, "OK"), cts.Token);
            await conn.WriteFrameAsync(FrameType.StreamData, 1, MakeStreamData(4, "bye\r\n"), cts.Token);

            Frame credit = (await conn.ReadFrameAsync(cts.Token))!.Value;
            Assert.Equal(FrameType.Credit, credit.Header.Type);
            Assert.Equal(1UL, credit.Header.StreamId);
            readCreditAck = StreamPayload.ReadOffset(credit.Payload);
        }, cts.Token);

        Task client = Task.Run(async () =>
        {
            await using Stream s = await endpoint.ConnectAsync(5000, cts.Token);
            await using var conn = new FrameConnection(s);

            JsonElement hp = JsonSerializer.SerializeToElement(
                new HelloParams(1, "cli", "0.1.0", "dev"), CoveJsonContext.Default.HelloParams);
            await conn.WriteFrameAsync(FrameType.Request, 0,
                ControlCodec.Encode(new ControlRequest("1", "cove://sys/hello", hp)), cts.Token);
            Frame helloResp = (await conn.ReadFrameAsync(cts.Token))!.Value;
            ControlResponse hr = ControlCodec.DecodeResponse(helloResp.Payload);
            Assert.True(hr.Ok);
            Assert.Equal(4242, hr.Data!.Value.GetProperty("enginePid").GetInt32());

            await conn.WriteFrameAsync(FrameType.Request, 0,
                ControlCodec.Encode(new ControlRequest("2", "cove://sys/ping")), cts.Token);
            Frame pong = (await conn.ReadFrameAsync(cts.Token))!.Value;
            ControlResponse pr = ControlCodec.DecodeResponse(pong.Payload);
            Assert.True(pr.Data!.Value.GetProperty("pong").GetBoolean());

            var nook1 = new List<byte>();
            var nook2 = new List<byte>();
            ulong next1 = 0, next2 = 0;
            for (int i = 0; i < 3; i++)
            {
                Frame f = (await conn.ReadFrameAsync(cts.Token))!.Value;
                Assert.Equal(FrameType.StreamData, f.Header.Type);
                ulong offset = StreamPayload.ReadStreamDataOffset(f.Payload);
                byte[] raw = StreamPayload.ReadStreamDataRaw(f.Payload).ToArray();
                if (f.Header.StreamId == 1)
                {
                    Assert.Equal(next1, offset);
                    next1 += (ulong)raw.Length;
                    nook1.AddRange(raw);
                }
                else
                {
                    Assert.Equal(2UL, f.Header.StreamId);
                    Assert.Equal(next2, offset);
                    next2 += (ulong)raw.Length;
                    nook2.AddRange(raw);
                }
            }
            Assert.Equal("hi\r\nbye\r\n", Encoding.ASCII.GetString(nook1.ToArray()));
            Assert.Equal("OK", Encoding.ASCII.GetString(nook2.ToArray()));

            byte[] creditPayload = new byte[8];
            StreamPayload.WriteOffset(creditPayload, next1);
            await conn.WriteFrameAsync(FrameType.Credit, 1, creditPayload, cts.Token);
        }, cts.Token);

        await Task.WhenAll(server, client);
        Assert.Equal(9UL, readCreditAck);

        try { Directory.Delete(dir, recursive: true); } catch { }
    }

    private static byte[] MakeStreamData(ulong offset, string text)
    {
        byte[] raw = Encoding.ASCII.GetBytes(text);
        byte[] dst = new byte[8 + raw.Length];
        StreamPayload.WriteStreamData(dst, offset, raw);
        return dst;
    }
}
