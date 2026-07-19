using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Cove.Protocol;
using Cove.Tui.Attach;
using Xunit;

namespace Cove.Tui.Tests;

public sealed class AttachSessionTests
{
    [Fact]
    public async Task SubscribeAsync_SendsSubscribeRequestBeforeWaitingForResponse()
    {
        var (clientSocket, serverSocket) = await ConnectLoopbackAsync();
        using (clientSocket)
        using (serverSocket)
        await using (var clientConnection = new FrameConnection(clientSocket.GetStream()))
        await using (var serverConnection = new FrameConnection(serverSocket.GetStream()))
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            var serverTask = CompleteSubscriptionAsync(serverConnection, timeout.Token);
            var session = new AttachSession(clientConnection, "nook-1", "tui", "0.1.0", "dev", "user:tui", "test-control-token");

            var result = await session.SubscribeAsync(timeout.Token);
            var requests = await serverTask;

            Assert.Equal("h", requests.Hello.Id);
            Assert.Equal("cove://sys/hello", requests.Hello.Uri);
            var hello = requests.Hello.Params!.Value.Deserialize(
                CoveJsonContext.Default.HelloParams);
            Assert.Equal("test-control-token", hello?.ControlToken);
            Assert.Equal("s", requests.Subscribe.Id);
            Assert.Equal("cove://commands/nook.subscribe", requests.Subscribe.Uri);
            Assert.Equal(7UL, result.StreamId);
        }
    }

    [Fact]
    public async Task PumpAsync_StaleStreamData_DoesNotRegressAckOffsetOrCredit()
    {
        var (clientSocket, serverSocket) = await ConnectLoopbackAsync();
        using (clientSocket)
        using (serverSocket)
        await using (var clientConnection = new FrameConnection(clientSocket.GetStream()))
        await using (var serverConnection = new FrameConnection(serverSocket.GetStream()))
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            var serverTask = CompleteSubscriptionAsync(serverConnection, timeout.Token);
            var session = new AttachSession(clientConnection, "nook-1", "tui", "0.1.0", "dev", "user:tui", "test-control-token");
            await session.SubscribeAsync(timeout.Token);
            await serverTask;

            var pumpTask = session.PumpAsync(
                (_, _) => Task.CompletedTask,
                (_, _) => Task.CompletedTask,
                (_, _) => Task.CompletedTask,
                timeout.Token);

            await serverConnection.WriteFrameAsync(
                FrameType.StreamData,
                7,
                StreamDataPayload(100, new byte[10]),
                timeout.Token);
            await serverConnection.WriteFrameAsync(
                FrameType.StreamData,
                7,
                StreamDataPayload(50, new byte[10]),
                timeout.Token);
            await serverConnection.WriteFrameAsync(
                FrameType.StreamEnd,
                7,
                StreamEndPayload(110, 0),
                timeout.Token);

            await pumpTask;
            var firstCredit = (await serverConnection.ReadFrameAsync(timeout.Token))!.Value;
            var secondCredit = (await serverConnection.ReadFrameAsync(timeout.Token))!.Value;

            Assert.Equal(FrameType.Credit, firstCredit.Header.Type);
            Assert.Equal(FrameType.Credit, secondCredit.Header.Type);
            Assert.Equal(110UL, BinaryPrimitives.ReadUInt64LittleEndian(firstCredit.Payload));
            Assert.Equal(110UL, BinaryPrimitives.ReadUInt64LittleEndian(secondCredit.Payload));
            Assert.Equal(110UL, session.AckedOffset);
        }
    }

    private static async Task<(ControlRequest Hello, ControlRequest Subscribe)> CompleteSubscriptionAsync(
        FrameConnection connection,
        CancellationToken ct)
    {
        var helloFrame = (await connection.ReadFrameAsync(ct))!.Value;
        var hello = ControlCodec.DecodeRequest(helloFrame.Payload);
        await connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(new ControlResponse(hello.Id, true)),
            ct);

        var subscribeFrame = (await connection.ReadFrameAsync(ct))!.Value;
        var subscribe = ControlCodec.DecodeRequest(subscribeFrame.Payload);
        var result = JsonSerializer.SerializeToElement(
            new SubscribeResult(7, 100, 4096),
            CoveJsonContext.Default.SubscribeResult);
        await connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(new ControlResponse(subscribe.Id, true, result)),
            ct);
        return (hello, subscribe);
    }

    private static async Task<(TcpClient Client, TcpClient Server)> ConnectLoopbackAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var client = new TcpClient();
            var connectTask = client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
            var server = await listener.AcceptTcpClientAsync();
            await connectTask;
            return (client, server);
        }
        finally
        {
            listener.Stop();
        }
    }

    private static byte[] StreamDataPayload(ulong offset, byte[] raw)
    {
        var payload = new byte[8 + raw.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(payload, offset);
        raw.CopyTo(payload, 8);
        return payload;
    }

    private static byte[] StreamEndPayload(ulong finalOffset, int exitCode)
    {
        var payload = new byte[12];
        BinaryPrimitives.WriteUInt64LittleEndian(payload, finalOffset);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8), exitCode);
        return payload;
    }
}
