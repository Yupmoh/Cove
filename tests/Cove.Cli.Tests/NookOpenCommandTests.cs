using System.Text.Json;
using Cove.Engine.Daemon;
using Cove.Platform;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Cove.Testing;
using Xunit;

namespace Cove.Cli.Tests;

[Collection(CliCollectionFixture.Name)]
public sealed class NookOpenCommandTests
{
    [Fact]
    public async Task TerminalWithoutCommand_MapsDefaultShellRequest()
    {
        var result = await InvokeAsync([
            "terminal",
            "--relative-to",
            "nook-anchor",
            "--placement",
            "right",
            "--cwd",
            "/repo",
            "--cols",
            "120",
            "--rows",
            "40",
            "--json",
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("cove://commands/nook.open", result.Request.Uri);
        var parameters = result.Request.Params!.Value;
        Assert.Equal("terminal", parameters.GetProperty("nookType").GetString());
        Assert.False(parameters.TryGetProperty("command", out _));
        Assert.Empty(parameters.GetProperty("args").EnumerateArray());
        Assert.Equal("nook-anchor", parameters.GetProperty("relativeToNookId").GetString());
        Assert.Equal("right", parameters.GetProperty("placement").GetString());
        Assert.Equal("/repo", parameters.GetProperty("cwd").GetString());
        Assert.Equal(120, parameters.GetProperty("cols").GetInt32());
        Assert.Equal(40, parameters.GetProperty("rows").GetInt32());
    }

    [Fact]
    public async Task TerminalCommand_PreservesRepeatedArgumentsInOrder()
    {
        var result = await InvokeAsync([
            "terminal",
            "--command",
            "/bin/sh",
            "--arg",
            "-lc",
            "--arg",
            "printf '%s' 'hello world'",
            "--placement",
            "below",
            "--bay-id",
            "bay-1",
            "--json",
        ]);

        Assert.Equal(0, result.ExitCode);
        var parameters = result.Request.Params!.Value;
        Assert.Equal("/bin/sh", parameters.GetProperty("command").GetString());
        Assert.Equal(
            ["-lc", "printf '%s' 'hello world'"],
            parameters.GetProperty("args").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("below", parameters.GetProperty("placement").GetString());
        Assert.Equal("bay-1", parameters.GetProperty("bayId").GetString());
    }

    private static async Task<CommandResult> InvokeAsync(string[] args)
    {
        var root = TestDirectory.Create(
            "nook-open-cli-",
            OperatingSystem.IsWindows() ? null : "/tmp");
        try
        {
            var paths = new DaemonPaths(
                CoveDataDir.ForRoot(CoveChannel.Stable, root));
            var endpoint = ControlEndpointFactory.FromSocketPath(
                paths.DataDir.SocketPath);
            Directory.CreateDirectory(Path.GetDirectoryName(paths.DataDir.SocketPath)!);
            File.WriteAllText(paths.ControlTokenPath, "nook-open-cli-token");
            await using var listener = endpoint.Bind();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var server = ServeAsync(listener, cancellation.Token);
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

            var exitCode = await NookCommands.NookOpen(context);
            var request = await server;
            return new CommandResult(exitCode, request);
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
        await using var stream = await listener.AcceptAsync(cancellationToken);
        await using var connection = new FrameConnection(stream);
        var hello = (await connection.ReadFrameAsync(cancellationToken))!.Value;
        var helloRequest = ControlCodec.DecodeRequest(hello.Payload);
        await connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(new ControlResponse(helloRequest.Id, true)),
            cancellationToken);
        var frame = (await connection.ReadFrameAsync(cancellationToken))!.Value;
        var request = ControlCodec.DecodeRequest(frame.Payload);
        var response = new NookOpenResult(
            "nook-new",
            "terminal",
            "bay-1",
            "shore-1",
            "right");
        await connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(new ControlResponse(
                request.Id,
                true,
                JsonSerializer.SerializeToElement(
                    response,
                    CoveJsonContext.Default.NookOpenResult))),
            cancellationToken);
        return request;
    }

    private sealed record CommandResult(
        int ExitCode,
        ControlRequest Request);
}
