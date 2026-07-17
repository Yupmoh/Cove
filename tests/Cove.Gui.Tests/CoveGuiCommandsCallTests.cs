using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Gui.Tests;

public sealed class CoveGuiCommandsCallTests
{
    [Fact]
    public async Task Call_WhenEngineRejects_ThrowsEngineError()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeNookListAsync(id => new ControlResponse(
                id,
                false,
                null,
                new ControlError("access_denied", "nook access denied"))));

        Assert.Equal("nook access denied", exception.Message);
    }

    [Fact]
    public async Task Call_WhenEngineSucceeds_ReturnsData()
    {
        const string json = "{\"nooks\":[{\"id\":\"nook-1\"}]}";

        var result = await InvokeNookListAsync(id => new ControlResponse(
            id,
            true,
            JsonDocument.Parse(json).RootElement.Clone()));

        Assert.Equal(json, result);
    }

    private static async Task<string> InvokeNookListAsync(Func<string, ControlResponse> responseFactory)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;

        var server = Task.Run(async () =>
        {
            try
            {
                using var client = await listener.AcceptTcpClientAsync(cancellation.Token);
                await using var connection = new FrameConnection(client.GetStream());

                var helloFrame = (await connection.ReadFrameAsync(cancellation.Token))!.Value;
                var helloRequest = ControlCodec.DecodeRequest(helloFrame.Payload);
                var helloData = JsonSerializer.SerializeToElement(
                    new HelloResult(ProtocolConstants.SemanticProtocolVersion, "0.4.0", 1, "dev"),
                    CoveJsonContext.Default.HelloResult);
                await connection.WriteFrameAsync(
                    FrameType.Response,
                    0,
                    ControlCodec.Encode(new ControlResponse(helloRequest.Id, true, helloData)),
                    cancellation.Token);

                var commandFrame = (await connection.ReadFrameAsync(cancellation.Token))!.Value;
                var commandRequest = ControlCodec.DecodeRequest(commandFrame.Payload);
                Assert.Equal("cove://commands/nook.list", commandRequest.Uri);
                await connection.WriteFrameAsync(
                    FrameType.Response,
                    0,
                    ControlCodec.Encode(responseFactory(commandRequest.Id)),
                    cancellation.Token);
            }
            finally
            {
                listener.Stop();
            }
        }, cancellation.Token);

        await using var link = new EngineLink(
            async ct =>
            {
                var client = new TcpClient();
                await client.ConnectAsync(endpoint.Address, endpoint.Port, ct);
                return client.GetStream();
            },
            "0.4.0",
            "dev");
        var commands = new CoveGuiCommands(link, NullLogger<CoveGuiCommands>.Instance, null!);

        try
        {
            return await commands.NookList(cancellation.Token);
        }
        finally
        {
            await server;
        }
    }
}
