using System.Buffers.Binary;
using System.Net.WebSockets;
using System.Text;
using Cove.Gui;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class PtyFrameValidationTests
{
    [Fact]
    public async Task PumpAsync_StreamDataPayloadShorterThanOffset_ThrowsInvalidDataException()
    {
        await AssertTruncatedPayloadThrowsInvalidDataException(FrameType.StreamData, new byte[7]);
    }

    [Fact]
    public async Task PumpAsync_ResyncPayloadShorterThanBaseOffset_ThrowsInvalidDataException()
    {
        await AssertTruncatedPayloadThrowsInvalidDataException(FrameType.Resync, new byte[7]);
    }

    [Fact]
    public async Task PumpAsync_StreamEndPayloadShorterThanFinalOffsetAndCode_ThrowsInvalidDataException()
    {
        await AssertTruncatedPayloadThrowsInvalidDataException(FrameType.StreamEnd, new byte[11]);
    }

    [Fact]
    public async Task PumpAsync_WellFormedStreamData_DecodesOffsetAndPayload()
    {
        await using var engine = new FakeEngine();
        byte[] payload = new byte[11];
        BinaryPrimitives.WriteUInt64LittleEndian(payload, 42);
        "pty"u8.CopyTo(payload.AsSpan(8));
        var serve = engine.ServeOnceAsync(0, 0, async stream =>
        {
            await WriteFrameAsync(stream, FrameType.StreamData, payload);
            await FakeEngine.WriteEnd(stream, 45, 0);
        });
        await using var client = await PtyStreamClient.SubscribeAsync(
            engine.Dial,
            "0.1.0",
            "dev",
            "nook",
            0,
            "test-control-token",
            CancellationToken.None);

        ulong decodedOffset = 0;
        byte[]? decodedPayload = null;
        await client.PumpAsync(
            (data, _) =>
            {
                decodedOffset = data.Offset;
                decodedPayload = data.Data.ToArray();
                return Task.CompletedTask;
            },
            (_, _) => Task.CompletedTask,
            (_, _) => Task.CompletedTask,
            CancellationToken.None);

        Assert.Equal(42UL, decodedOffset);
        Assert.Equal("pty"u8.ToArray(), decodedPayload);
        await serve;
    }

    [Fact]
    public async Task SubscribeAsync_PreservesAuthoritativeInitialResyncMetadata()
    {
        await using var engine = new FakeEngine();
        var serve = engine.ServeOnceAsync(7, 7, stream => FakeEngine.WriteEnd(stream, 7, 0), authoritativeInitialResync: true);
        await using var client = await PtyStreamClient.SubscribeAsync(
            engine.Dial,
            "0.1.0",
            "dev",
            "nook",
            99,
            "test-control-token",
            CancellationToken.None);

        Assert.True(client.AuthoritativeInitialResync);
        Assert.Equal(7UL, client.BaseOffset);
        await client.PumpAsync((_, _) => Task.CompletedTask, (_, _) => Task.CompletedTask, (_, _) => Task.CompletedTask, CancellationToken.None);
        await serve;
    }

    [Fact]
    public async Task RunAsync_AuthoritativeInitialResync_SendsExistingResyncControlFirst()
    {
        await using var engine = new FakeEngine();
        var serve = engine.ServeOnceAsync(
            7,
            7,
            stream => FakeEngine.WriteEnd(stream, 7, 0),
            terminalModePreambleBase64: "bW9kZXM=",
            authoritativeInitialResync: true,
            terminalCheckpointBase64: "Y2hlY2twb2ludA==",
            checkpointCols: 132,
            checkpointRows: 40);
        using var socket = new RecordingWebSocket();

        await PtyWsHandler.RunAsync(socket, engine.Dial, "0.1.0", "dev", "nook", 99, "test-control-token", NullLogger.Instance, CancellationToken.None);

        Assert.Equal("{\"t\":\"resync\",\"base\":7,\"modes\":\"bW9kZXM=\",\"checkpoint\":\"Y2hlY2twb2ludA==\",\"checkpointCols\":132,\"checkpointRows\":40}", socket.SentText[0]);
        await serve;
    }

    private static async Task AssertTruncatedPayloadThrowsInvalidDataException(FrameType type, byte[] payload)
    {
        await using var engine = new FakeEngine();
        var serve = engine.ServeOnceAsync(0, 0, stream => WriteFrameAsync(stream, type, payload));
        await using var client = await PtyStreamClient.SubscribeAsync(
            engine.Dial,
            "0.1.0",
            "dev",
            "nook",
            0,
            "test-control-token",
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => client.PumpAsync(
            (_, _) => Task.CompletedTask,
            (_, _) => Task.CompletedTask,
            (_, _) => Task.CompletedTask,
            CancellationToken.None));
        Assert.Contains(type.ToString(), exception.Message);
        Assert.Contains(payload.Length.ToString(), exception.Message);
        await serve;
    }

    private static async Task WriteFrameAsync(Stream stream, FrameType type, byte[] payload)
    {
        byte[] frame = new byte[ProtocolConstants.HeaderSize + payload.Length];
        FrameHeader.Write(frame, new FrameHeader(type, 1, 1, (uint)payload.Length));
        payload.CopyTo(frame, ProtocolConstants.HeaderSize);
        await stream.WriteAsync(frame);
        await stream.FlushAsync();
    }

    private sealed class RecordingWebSocket : WebSocket
    {
        private WebSocketState _state = WebSocketState.Open;
        public List<string> SentText { get; } = [];
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => _state;
        public override string? SubProtocol => null;
        public override void Abort() => _state = WebSocketState.Aborted;
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => CloseAsync(closeStatus, statusDescription, cancellationToken);
        public override void Dispose() => _state = WebSocketState.Closed;
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            _state = WebSocketState.CloseReceived;
            return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
        }
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            if (messageType == WebSocketMessageType.Text)
                SentText.Add(Encoding.UTF8.GetString(buffer));
            return Task.CompletedTask;
        }
    }
}
