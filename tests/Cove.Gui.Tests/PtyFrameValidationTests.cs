using System.Buffers.Binary;
using Cove.Gui;
using Cove.Protocol;
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
}
