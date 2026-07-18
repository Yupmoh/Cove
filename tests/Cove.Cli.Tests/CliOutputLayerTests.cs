using Cove.Cli;
using Xunit;

namespace Cove.Cli.Tests;

[Collection(CliCollectionFixture.Name)]
public sealed class CliOutputLayerTests
{
    private static CommandContext NewContext(StringWriter stdout, StringWriter stderr, string[] args)
    {
        var tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-cli-test-" + System.Guid.NewGuid().ToString("N"));
        var prev = System.Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        System.Environment.SetEnvironmentVariable("COVE_DATA_DIR", tempRoot);
        try
        {
            var dataDir = Cove.Platform.CoveDataDir.Resolve(Cove.Platform.CoveChannel.Stable);
            var paths = new Cove.Engine.Daemon.DaemonPaths(dataDir);
            var endpoint = Cove.Platform.Ipc.ControlEndpointFactory.FromSocketPath(System.IO.Path.Combine(tempRoot, "test.sock"));
            return new CommandContext(paths, endpoint, stdout, stderr, args);
        }
        finally { System.Environment.SetEnvironmentVariable("COVE_DATA_DIR", prev); }
    }

    [Fact]
    public void IsJson_TrueWhenDashDashJsonPresent()
    {
        var ctx = NewContext(new StringWriter(), new StringWriter(), new[] { "--json" });
        Assert.True(ctx.IsJson);
    }

    [Fact]
    public void IsJson_FalseWhenAbsent()
    {
        var ctx = NewContext(new StringWriter(), new StringWriter(), System.Array.Empty<string>());
        Assert.False(ctx.IsJson);
    }

    [Fact]
    public void Filter_ExtractsColumnValuePair()
    {
        var ctx = NewContext(new StringWriter(), new StringWriter(), new[] { "--filter", "name=foo" });
        Assert.Equal("name=foo", ctx.Filter);
    }

    [Fact]
    public void Filter_NullWhenAbsent()
    {
        var ctx = NewContext(new StringWriter(), new StringWriter(), System.Array.Empty<string>());
        Assert.Null(ctx.Filter);
    }

    [Fact]
    public void Source_DefaultsToUserCli()
    {
        var ctx = NewContext(new StringWriter(), new StringWriter(), System.Array.Empty<string>());
        Assert.Equal("user:cli", ctx.Source);
    }

    [Fact]
    public void Source_OverriddenByDashDashSource()
    {
        var ctx = NewContext(new StringWriter(), new StringWriter(), new[] { "--source", "agent:p1" });
        Assert.Equal("agent:p1", ctx.Source);
    }

    [Fact]
    public void Channel_DefaultsStable()
    {
        var ctx = NewContext(new StringWriter(), new StringWriter(), System.Array.Empty<string>());
        Assert.Equal(Cove.Platform.CoveChannel.Stable, ctx.Channel);
    }

    [Fact]
    public void Channel_OverriddenByDashDashChannel()
    {
        var ctx = NewContext(new StringWriter(), new StringWriter(), new[] { "--channel", "dev" });
        Assert.Equal(Cove.Platform.CoveChannel.Dev, ctx.Channel);
    }
    [Fact]
    public void RenderJson_WritesDataToStdout()
    {
        var stdout = new StringWriter();
        var ctx = NewContext(stdout, new StringWriter(), new[] { "--json" });
        var data = System.Text.Json.JsonSerializer.SerializeToElement(new { name = "test", count = 5 });
        ctx.Render(data);
        var output = stdout.ToString().Trim();
        Assert.Contains("\"name\"", output);
        Assert.Contains("\"count\"", output);
        Assert.Equal("test", System.Text.Json.JsonDocument.Parse(output).RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void RenderStatus_WritesToStderrNotStdout()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var ctx = NewContext(stdout, stderr, System.Array.Empty<string>());
        ctx.RenderStatus("operation complete");
        Assert.Equal("", stdout.ToString());
        Assert.Contains("operation complete", stderr.ToString());
    }

    [Fact]
    public async Task ConfigSet_RoutesToDaemonWithoutWritingConfigFile()
    {
        var root = System.IO.Path.Combine("/tmp", "cr-" + System.Guid.NewGuid().ToString("N").Substring(0, 8));
        System.IO.Directory.CreateDirectory(root);
        try
        {
            var result = await InvokeAgainstDaemonAsync(
                root,
                new[] { "terminal.fontFamily", "Berkeley Mono" },
                CliCommands.ConfigSet);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("cove://commands/config.set", result.Request.Uri);
            Assert.Equal("terminal.fontFamily", result.Request.Params!.Value.GetProperty("key").GetString());
            Assert.Equal("Berkeley Mono", result.Request.Params.Value.GetProperty("value").GetString());
            Assert.False(System.IO.File.Exists(System.IO.Path.Combine(root, "config.json")));
            Assert.Contains("set terminal.fontFamily = Berkeley Mono", result.Stdout);
            Assert.Equal("", result.Stderr);
        }
        finally
        {
            try { System.IO.Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public async Task ConfigGet_NotFoundIsSuccessfulAndPrintsNothing()
    {
        var root = System.IO.Path.Combine("/tmp", "cr-" + System.Guid.NewGuid().ToString("N").Substring(0, 8));
        System.IO.Directory.CreateDirectory(root);
        try
        {
            var result = await InvokeAgainstDaemonAsync(
                root,
                new[] { "terminal.fontFamily" },
                CliCommands.ConfigGet,
                request => new Cove.Protocol.ControlResponse(
                    request.Id,
                    false,
                    Error: new Cove.Protocol.ControlError("not_found", "config key not found")));

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("cove://commands/config.get", result.Request.Uri);
            Assert.Equal("terminal.fontFamily", result.Request.Params!.Value.GetProperty("key").GetString());
            Assert.Equal("", result.Stdout);
            Assert.Equal("", result.Stderr);
        }
        finally
        {
            try { System.IO.Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public async Task ExtensionList_RoutesToDaemonAndFormatsResponse()
    {
        var root = System.IO.Path.Combine("/tmp", "cr-" + System.Guid.NewGuid().ToString("N").Substring(0, 8));
        System.IO.Directory.CreateDirectory(root);
        try
        {
            var result = await InvokeAgainstDaemonAsync(
                root,
                System.Array.Empty<string>(),
                CliCommands.ExtensionList,
                request =>
                {
                    using var document = System.Text.Json.JsonDocument.Parse(
                        """[{"command":"extension.example.run","source":"adapter","adapter":"example","method":"run"}]""");
                    return new Cove.Protocol.ControlResponse(request.Id, true, document.RootElement.Clone());
                });

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("cove://commands/extension.list", result.Request.Uri);
            Assert.Contains("extension.example.run  (adapter: example, method: run)", result.Stdout);
            Assert.Contains("Total: 1", result.Stdout);
            Assert.Equal("", result.Stderr);
        }
        finally
        {
            try { System.IO.Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public async Task CaptureStop_QuotedIdProducesValidJsonPayload()
    {
        var root = System.IO.Path.Combine("/tmp", "cr-" + System.Guid.NewGuid().ToString("N").Substring(0, 8));
        System.IO.Directory.CreateDirectory(root);
        try
        {
            const string id = "capture-\"quoted\\path";
            var result = await InvokeAgainstDaemonAsync(root, new[] { "--id", id }, CliCommands.CaptureStop);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("cove://commands/capture.stop", result.Request.Uri);
            Assert.Equal(id, result.Request.Params!.Value.GetProperty("id").GetString());
            Assert.Equal("", result.Stderr);
        }
        finally
        {
            try { System.IO.Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public async Task DiagnosticsSnapshot_NumericOptionsProduceNumberProperties()
    {
        var root = System.IO.Path.Combine("/tmp", "cr-" + System.Guid.NewGuid().ToString("N").Substring(0, 8));
        System.IO.Directory.CreateDirectory(root);
        try
        {
            var result = await InvokeAgainstDaemonAsync(
                root,
                new[] { "--nooks", "2", "--bays", "3", "--agents", "5" },
                CliCommands.DiagnosticsSnapshot);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("cove://commands/diagnostics.snapshot.take", result.Request.Uri);
            var parameters = result.Request.Params!.Value;
            Assert.Equal(System.Text.Json.JsonValueKind.Number, parameters.GetProperty("activeNooks").ValueKind);
            Assert.Equal(2, parameters.GetProperty("activeNooks").GetInt32());
            Assert.Equal(3, parameters.GetProperty("activeBays").GetInt32());
            Assert.Equal(5, parameters.GetProperty("activeAgents").GetInt32());
            Assert.Equal("", result.Stderr);
        }
        finally
        {
            try { System.IO.Directory.Delete(root, true); } catch { }
        }
    }

    private static async Task<(int ExitCode, Cove.Protocol.ControlRequest Request, string Stdout, string Stderr)> InvokeAgainstDaemonAsync(
        string root,
        string[] args,
        Func<CommandContext, Task<int>> invoke,
        Func<Cove.Protocol.ControlRequest, Cove.Protocol.ControlResponse>? respond = null)
    {
        var paths = new Cove.Engine.Daemon.DaemonPaths(Cove.Platform.CoveDataDir.ForRoot(Cove.Platform.CoveChannel.Stable, root));
        var endpoint = Cove.Platform.Ipc.ControlEndpointFactory.FromSocketPath(paths.DataDir.SocketPath);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(paths.DataDir.SocketPath)!);
        await using var listener = endpoint.Bind();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var server = Task.Run(async () =>
        {
            await using var stream = await listener.AcceptAsync(cts.Token);
            await using var connection = new Cove.Protocol.FrameConnection(stream);
            var hello = (await connection.ReadFrameAsync(cts.Token))!.Value;
            var helloRequest = Cove.Protocol.ControlCodec.DecodeRequest(hello.Payload);
            await connection.WriteFrameAsync(
                Cove.Protocol.FrameType.Response,
                0,
                Cove.Protocol.ControlCodec.Encode(new Cove.Protocol.ControlResponse(helloRequest.Id, true)),
                cts.Token);
            var frame = (await connection.ReadFrameAsync(cts.Token))!.Value;
            var request = Cove.Protocol.ControlCodec.DecodeRequest(frame.Payload);
            await connection.WriteFrameAsync(
                Cove.Protocol.FrameType.Response,
                0,
                Cove.Protocol.ControlCodec.Encode(
                    respond?.Invoke(request) ?? new Cove.Protocol.ControlResponse(request.Id, true)),
                cts.Token);
            return request;
        }, cts.Token);
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var context = new CommandContext(paths, endpoint, stdout, stderr, args);
        var exitCode = await invoke(context);
        var request = await server;
        return (exitCode, request, stdout.ToString(), stderr.ToString());
    }
}
