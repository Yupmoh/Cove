using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Gui.Tests;

public sealed class CheckpointIpcTests
{
    [Fact]
    public async Task Checkpoint_uses_background_connection_and_preserves_exact_utf8()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var checkpointConnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCheckpointRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var interactiveRequestReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serializedVt = "\u001b\0\u263a\U0001f642" + new string('x', 4 * 1024 * 1024);

        Task server = ServeAsync(
            listener,
            checkpointConnected,
            allowCheckpointRead,
            interactiveRequestReceived,
            serializedVt,
            cancellation.Token);

        Func<CancellationToken, Task<Stream>> dial = async ct =>
        {
            var client = new TcpClient();
            await client.ConnectAsync(endpoint.Address, endpoint.Port, ct);
            return client.GetStream();
        };

        await using var interactiveLink = new EngineLink(dial, "0.4.0", "dev", "test-control-token");
        await using var checkpointLink = interactiveLink.CreateBackgroundLink();
        var commands = new CoveGuiCommands(
            interactiveLink,
            checkpointLink,
            NullLogger<CoveGuiCommands>.Instance,
            null!,
            new MediaLeaseRegistry());

        try
        {
            Task<string> checkpoint = commands.NookCheckpoint(
                "nook-1",
                serializedVt,
                42,
                120,
                40,
                10_000,
                cancellation.Token).AsTask();

            await checkpointConnected.Task.WaitAsync(cancellation.Token);
            Task<string> write = commands.NookWrite(
                "nook-1",
                Convert.ToBase64String([0x03]),
                cancellation.Token).AsTask();

            await interactiveRequestReceived.Task.WaitAsync(cancellation.Token);
            Assert.False(checkpoint.IsCompleted);
            Assert.Equal("{}", await write.WaitAsync(cancellation.Token));

            allowCheckpointRead.SetResult();
            Assert.Equal("{}", await checkpoint.WaitAsync(cancellation.Token));
            await server.WaitAsync(cancellation.Token);
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task ServeAsync(
        TcpListener listener,
        TaskCompletionSource checkpointConnected,
        TaskCompletionSource allowCheckpointRead,
        TaskCompletionSource interactiveRequestReceived,
        string expectedSerializedVt,
        CancellationToken cancellationToken)
    {
        using TcpClient checkpointClient = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var checkpointConnection = new FrameConnection(checkpointClient.GetStream());
        await CompleteHelloAsync(checkpointConnection, cancellationToken);
        checkpointConnected.SetResult();

        using TcpClient interactiveClient = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var interactiveConnection = new FrameConnection(interactiveClient.GetStream());
        await CompleteHelloAsync(interactiveConnection, cancellationToken);

        Frame writeFrame = (await interactiveConnection.ReadFrameAsync(cancellationToken))!.Value;
        ControlRequest writeRequest = ControlCodec.DecodeRequest(writeFrame.Payload);
        Assert.Equal("cove://commands/nook.write", writeRequest.Uri);
        interactiveRequestReceived.SetResult();
        await RespondAsync(interactiveConnection, writeRequest.Id, cancellationToken);

        await allowCheckpointRead.Task.WaitAsync(cancellationToken);
        Frame checkpointFrame = (await checkpointConnection.ReadFrameAsync(cancellationToken))!.Value;
        ControlRequest checkpointRequest = ControlCodec.DecodeRequest(checkpointFrame.Payload);
        Assert.Equal("cove://commands/nook.checkpoint", checkpointRequest.Uri);
        NookCheckpointParams parameters = JsonSerializer.Deserialize(
            checkpointRequest.Params!.Value,
            CoveJsonContext.Default.NookCheckpointParams)!;
        Assert.Equal(Encoding.UTF8.GetBytes(expectedSerializedVt), Convert.FromBase64String(parameters.DataBase64));
        Assert.Equal(42, parameters.Offset);
        Assert.Equal(120, parameters.Cols);
        Assert.Equal(40, parameters.Rows);
        Assert.Equal(10_000, parameters.ScrollbackLines);
        await RespondAsync(checkpointConnection, checkpointRequest.Id, cancellationToken);
    }

    private static async Task CompleteHelloAsync(
        FrameConnection connection,
        CancellationToken cancellationToken)
    {
        Frame helloFrame = (await connection.ReadFrameAsync(cancellationToken))!.Value;
        ControlRequest helloRequest = ControlCodec.DecodeRequest(helloFrame.Payload);
        HelloParams hello = JsonSerializer.Deserialize(
            helloRequest.Params!.Value,
            CoveJsonContext.Default.HelloParams)!;
        Assert.Equal("test-control-token", hello.ControlToken);
        JsonElement helloResult = JsonSerializer.SerializeToElement(
            new HelloResult(ProtocolConstants.SemanticProtocolVersion, "0.4.0", 1, "dev"),
            CoveJsonContext.Default.HelloResult);
        await connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(new ControlResponse(helloRequest.Id, true, helloResult)),
            cancellationToken);
    }

    private static ValueTask RespondAsync(
        FrameConnection connection,
        string requestId,
        CancellationToken cancellationToken)
        => connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(new ControlResponse(requestId, true)),
            cancellationToken);
}
