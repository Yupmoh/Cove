using System.Diagnostics;
using System.Text.Json;
using Cove.Protocol;
using Xunit;

namespace Cove.Cli.Tests;

[Collection(CliCollectionFixture.Name)]
public sealed class HookEmitCommandTests
{
    [Fact]
    public async Task EmptyRedirectedStdin_SendsEmptyPayloadAndWritesNoOutput()
    {
        var result = await RunAsync(
            "",
            "session-start",
            "--adapter",
            "claude-code",
            "--nook-id",
            "nook-cli-1");

        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(result.Request);
        Assert.Equal("cove://commands/hook.emit", result.Request!.Uri);
        var parameters = result.Request.Params!.Value;
        Assert.Equal("claude-code", parameters.GetProperty("adapter").GetString());
        Assert.Equal("session-start", parameters.GetProperty("event").GetString());
        Assert.Equal("nook-cli-1", parameters.GetProperty("nookId").GetString());
        Assert.Equal(JsonValueKind.Object, parameters.GetProperty("payload").ValueKind);
        Assert.Empty(parameters.GetProperty("payload").EnumerateObject());
        Assert.Equal("", result.Stdout);
        Assert.Equal("", result.Stderr);
    }

    [Fact]
    public async Task ValidRedirectedJson_IsForwardedAsHookPayload()
    {
        var result = await RunAsync(
            """{"prompt":"hello","count":2,"nested":{"ready":true}}""",
            "user-prompt-submit",
            "--adapter",
            "claude-code");

        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(result.Request);
        var payload = result.Request!.Params!.Value.GetProperty("payload");
        Assert.Equal("hello", payload.GetProperty("prompt").GetString());
        Assert.Equal(2, payload.GetProperty("count").GetInt32());
        Assert.True(payload.GetProperty("nested").GetProperty("ready").GetBoolean());
        Assert.Equal("", result.Stdout);
        Assert.Equal("", result.Stderr);
    }

    [Fact]
    public async Task MalformedRedirectedJson_IsRejectedBeforeDispatch()
    {
        var result = await RunAsync(
            "{\"broken\"",
            "session-start",
            "--adapter",
            "claude-code");

        Assert.Equal(1, result.ExitCode);
        Assert.Null(result.Request);
        Assert.Equal("", result.Stdout);
        Assert.Contains("invalid_params", result.Stderr);
    }

    [Fact]
    public async Task Verbose_WritesSuccessfulResponseToStderrOnly()
    {
        var result = await RunAsync(
            "{}",
            "session-start",
            "--adapter",
            "claude-code",
            "--verbose");

        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(result.Request);
        Assert.Equal("", result.Stdout);
        Assert.Equal("{}" + Environment.NewLine, result.Stderr);
    }

    private static async Task<CliResult> RunAsync(string stdin, params string[] commandArgs)
    {
        var root = Cove.Testing.TestDirectory.Create(
            "hc-",
            OperatingSystem.IsWindows() ? null : "/tmp");
        var paths = new Cove.Engine.Daemon.DaemonPaths(
            Cove.Platform.CoveDataDir.ForRoot(Cove.Platform.CoveChannel.Stable, root));
        var endpoint = Cove.Platform.Ipc.ControlEndpointFactory.FromSocketPath(paths.DataDir.SocketPath);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.DataDir.SocketPath)!);
        File.WriteAllText(paths.ControlTokenPath, "hook-test-control-token");
        var listener = endpoint.Bind();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var server = ServeAsync(listener, cts.Token);
        using var process = new Process
        {
            StartInfo = CreateStartInfo(root, commandArgs)
        };

        try
        {
            Assert.True(process.Start());
            await process.StandardInput.WriteAsync(stdin);
            process.StandardInput.Close();
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            cts.Cancel();
            ControlRequest? request;
            try
            {
                request = await server;
            }
            catch (OperationCanceledException)
            {
                request = null;
            }
            return new CliResult(process.ExitCode, request, stdout, stderr);
        }
        finally
        {
            cts.Cancel();
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await listener.DisposeAsync();
            Cove.Testing.TestDirectory.Delete(root);
        }
    }

    private static ProcessStartInfo CreateStartInfo(string root, string[] commandArgs)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(typeof(Cove.Cli.CommandContext).Assembly.Location);
        startInfo.ArgumentList.Add("hook");
        startInfo.ArgumentList.Add("emit");
        foreach (var argument in commandArgs)
            startInfo.ArgumentList.Add(argument);
        startInfo.Environment["COVE_DATA_DIR"] = root;
        return startInfo;
    }

    private static async Task<ControlRequest> ServeAsync(
        Cove.Platform.Ipc.IControlListener listener,
        CancellationToken cancellationToken)
    {
        await using var stream = await listener.AcceptAsync(cancellationToken);
        await using var connection = new FrameConnection(stream);
        var helloFrame = (await connection.ReadFrameAsync(cancellationToken))!.Value;
        var hello = ControlCodec.DecodeRequest(helloFrame.Payload);
        await connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(new ControlResponse(hello.Id, true)),
            cancellationToken);
        var requestFrame = (await connection.ReadFrameAsync(cancellationToken))!.Value;
        var request = ControlCodec.DecodeRequest(requestFrame.Payload);
        using var responseDocument = JsonDocument.Parse("{}");
        await connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(new ControlResponse(
                request.Id,
                true,
                responseDocument.RootElement.Clone())),
            cancellationToken);
        return request;
    }

    private sealed record CliResult(
        int ExitCode,
        ControlRequest? Request,
        string Stdout,
        string Stderr);
}
