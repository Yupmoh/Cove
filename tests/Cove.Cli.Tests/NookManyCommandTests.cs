using System.Text.Json;
using Cove.Engine.Daemon;
using Cove.Platform;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Cove.Testing;
using Xunit;

namespace Cove.Cli.Tests;

[Collection(CliCollectionFixture.Name)]
public sealed class NookManyCommandTests
{
    [Fact]
    public async Task OpenMany_PreservesOrderedTypedItems()
    {
        var result = await InvokeAsync(
            true,
            [
                "--item",
                "{\"nookType\":\"terminal\",\"command\":\"yazi\"}",
                "--item",
                "{\"nookType\":\"browser\",\"url\":\"https://example.com\"}",
                "--item",
                "{\"nookType\":\"agent\",\"adapter\":\"codex\",\"name\":\"Codex\"}",
                "--relative-to",
                "nook-anchor",
                "--placement",
                "right",
                "--balance",
                "below",
                "--json",
            ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("cove://commands/nook.open-many", result.Request.Uri);
        var parameters = result.Request.Params!.Value;
        Assert.Equal("nook-anchor", parameters.GetProperty("relativeToNookId").GetString());
        Assert.Equal("right", parameters.GetProperty("placement").GetString());
        Assert.Equal("below", parameters.GetProperty("balance").GetString());
        var items = parameters.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(["terminal", "browser", "agent"], items.Select(item => item.GetProperty("nookType").GetString()));
        Assert.Equal("yazi", items[0].GetProperty("command").GetString());
        Assert.Equal("https://example.com", items[1].GetProperty("url").GetString());
        Assert.Equal("codex", items[2].GetProperty("adapter").GetString());
    }

    [Fact]
    public async Task CloseOthers_DefaultsToSameShore()
    {
        var result = await InvokeAsync(
            false,
            ["nook-keep", "--json"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("cove://commands/nook.close-others", result.Request.Uri);
        Assert.Equal("nook-keep", result.Request.Params!.Value.GetProperty("nookId").GetString());
        Assert.Equal("same-shore", result.Request.Params!.Value.GetProperty("scope").GetString());
    }

    private static async Task<CommandResult> InvokeAsync(bool openMany, string[] args)
    {
        var root = TestDirectory.Create(
            "nook-many-cli-",
            OperatingSystem.IsWindows() ? null : "/tmp");
        try
        {
            var paths = new DaemonPaths(CoveDataDir.ForRoot(CoveChannel.Stable, root));
            var endpoint = ControlEndpointFactory.FromSocketPath(paths.DataDir.SocketPath);
            Directory.CreateDirectory(Path.GetDirectoryName(paths.DataDir.SocketPath)!);
            File.WriteAllText(paths.ControlTokenPath, "nook-many-cli-token");
            await using var listener = endpoint.Bind();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var server = ServeAsync(listener, cancellation.Token);
            var context = new CommandContext(
                paths,
                endpoint,
                new StringWriter(),
                new StringWriter(),
                args,
                CoveChannel.Stable,
                optionArgs: args);

            var exitCode = openMany
                ? await NookCommands.NookOpenMany(context)
                : await NookCommands.NookCloseOthers(context);
            return new CommandResult(exitCode, await server);
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
        JsonElement responseData;
        if (request.Uri == "cove://commands/nook.open-many")
        {
            responseData = JsonSerializer.SerializeToElement(
                new NookOpenManyResult([
                    new NookManyOpenedResult("nook-1", "terminal", null, "bay-1", "shore-1", "right"),
                ]),
                CoveJsonContext.Default.NookOpenManyResult);
        }
        else
        {
            responseData = JsonSerializer.SerializeToElement(
                new NookCloseOthersResult("nook-keep", []),
                CoveJsonContext.Default.NookCloseOthersResult);
        }
        await connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(new ControlResponse(request.Id, true, responseData)),
            cancellationToken);
        return request;
    }

    private sealed record CommandResult(int ExitCode, ControlRequest Request);
}
