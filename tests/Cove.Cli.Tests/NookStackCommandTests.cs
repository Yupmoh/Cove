using System.Text.Json;
using Cove.Engine.Daemon;
using Cove.Platform;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Cove.Testing;
using Xunit;

namespace Cove.Cli.Tests;

[Collection(CliCollectionFixture.Name)]
public sealed class NookStackCommandTests
{
    [Theory]
    [InlineData("left")]
    [InlineData("right")]
    [InlineData("above")]
    [InlineData("below")]
    public async Task NookStack_MapsTargetAndPlacement(
        string placement)
    {
        var root = TestDirectory.Create(
            "nook-stack-cli-",
            OperatingSystem.IsWindows() ? null : "/tmp");
        try
        {
            var paths = new DaemonPaths(
                CoveDataDir.ForRoot(CoveChannel.Stable, root));
            var endpoint = ControlEndpointFactory.FromSocketPath(
                paths.DataDir.SocketPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(paths.DataDir.SocketPath)!);
            File.WriteAllText(
                paths.ControlTokenPath,
                "nook-stack-cli-token");
            await using var listener = endpoint.Bind();
            using var cancellation = new CancellationTokenSource(
                TimeSpan.FromSeconds(15));
            var server = ServeAsync(listener, cancellation.Token);
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var args = new[]
            {
                "nook-target",
                "--placement",
                placement,
                "--json",
            };
            var context = new CommandContext(
                paths,
                endpoint,
                stdout,
                stderr,
                args,
                CoveChannel.Stable,
                optionArgs: args);

            var exitCode = await NookCommands.NookStack(context);
            var request = await server;

            Assert.Equal(0, exitCode);
            Assert.Equal("cove://commands/nook.stack", request.Uri);
            Assert.Equal(
                "nook-target",
                request.Params!.Value.GetProperty("nookId").GetString());
            Assert.Equal(
                placement,
                request.Params.Value.GetProperty("placement").GetString());
            Assert.Contains("\"nooks\":4", stdout.ToString());
            Assert.Equal("", stderr.ToString());
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Theory]
    [InlineData()]
    [InlineData("nook-target")]
    [InlineData("--placement", "below")]
    public async Task NookStack_MissingRequiredArgumentPrintsUsage(
        params string[] args)
    {
        var root = TestDirectory.Create(
            "nook-stack-cli-invalid-",
            OperatingSystem.IsWindows() ? null : "/tmp");
        try
        {
            var paths = new DaemonPaths(
                CoveDataDir.ForRoot(CoveChannel.Stable, root));
            var endpoint = ControlEndpointFactory.FromSocketPath(
                paths.DataDir.SocketPath);
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var context = new CommandContext(
                paths,
                endpoint,
                stdout,
                stderr,
                args,
                CoveChannel.Stable,
                optionArgs: args);

            var exitCode = await NookCommands.NookStack(context);

            Assert.Equal(1, exitCode);
            Assert.Contains(
                "usage: cove nook stack <nook-id> --placement <left|right|above|below>",
                stderr.ToString());
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    private static async Task<ControlRequest> ServeAsync(
        IControlListener listener,
        CancellationToken cancellationToken)
    {
        await using var stream = await listener.AcceptAsync(
            cancellationToken);
        await using var connection = new FrameConnection(stream);
        var hello = (await connection.ReadFrameAsync(
            cancellationToken))!.Value;
        var helloRequest = ControlCodec.DecodeRequest(hello.Payload);
        await connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(new ControlResponse(
                helloRequest.Id,
                true)),
            cancellationToken);
        var frame = (await connection.ReadFrameAsync(
            cancellationToken))!.Value;
        var request = ControlCodec.DecodeRequest(frame.Payload);
        await connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(new ControlResponse(
                request.Id,
                true,
                JsonSerializer.SerializeToElement(
                    new NookStackResult(
                        "nook-target",
                        "bay-1",
                        "shore-1",
                        "below",
                        4),
                    CoveJsonContext.Default.NookStackResult))),
            cancellationToken);
        return request;
    }
}
