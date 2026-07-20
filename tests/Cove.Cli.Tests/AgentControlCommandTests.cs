using System.Text.Json;
using Cove.Engine.Daemon;
using Cove.Platform;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Cove.Testing;
using Xunit;

namespace Cove.Cli.Tests;

[Collection(CliCollectionFixture.Name)]
public sealed class AgentControlCommandTests
{
    [Fact]
    public async Task AgentList_RendersHumanGolden()
    {
        var result = await InvokeAsync(
            AgentCommands.AgentList,
            [],
            AgentListResponse);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            Golden("agent-list.txt"),
            result.Stdout);
        Assert.Equal("", result.Stderr);
        Assert.Equal(
            "cove://commands/agent.list",
            result.Request.Uri);
        Assert.Equal(
            "same-tab",
            result.Request.Params!.Value
                .GetProperty("scope")
                .GetString());
    }

    [Fact]
    public async Task AgentList_RendersJsonGolden()
    {
        var result = await InvokeAsync(
            AgentCommands.AgentList,
            ["--json", "--scope", "all"],
            AgentListResponse);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            Golden("agent-list.json"),
            result.Stdout);
        Assert.Equal("", result.Stderr);
        Assert.Equal(
            "all",
            result.Request.Params!.Value
                .GetProperty("scope")
                .GetString());
    }

    [Fact]
    public async Task AgentMessage_ForwardsFreshTurnAndStableScopeError()
    {
        var result = await InvokeAsync(
            AgentCommands.AgentMessage,
            [
                "nook-target",
                "review this",
                "--submit-pause-ms",
                "75",
            ],
            request => new ControlResponse(
                request.Id,
                false,
                Error: new ControlError(
                    "access_denied",
                    "target is outside caller scope")));

        Assert.Equal(1, result.ExitCode);
        Assert.Equal("", result.Stdout);
        Assert.Equal(
            "error: access_denied: target is outside caller scope"
                + Environment.NewLine,
            result.Stderr);
        var parameters = result.Request.Params!.Value;
        Assert.Equal(
            "nook-target",
            parameters.GetProperty("target").GetString());
        Assert.Equal(
            "review this",
            parameters.GetProperty("body").GetString());
        Assert.False(
            parameters.GetProperty("noFrame").GetBoolean());
        Assert.Equal(
            75,
            parameters.GetProperty("submitPauseMs").GetInt32());
    }

    [Fact]
    public async Task AgentLaunchAndResume_MapStableParameters()
    {
        var launched = await InvokeAsync(
            AgentCommands.AgentLaunch,
            [
                "omp",
                "--profile",
                "fast",
                "--relative-to",
                "nook-a",
                "--placement",
                "below",
                "--yolo",
                "--model",
                "openrouter/model-x",
                "--effort",
                "high",
                "--access-scope",
                "same-tab",
            ],
            LaunchResponse);
        var resumed = await InvokeAsync(
            AgentCommands.AgentResume,
            [
                "claude-code",
                "session-7",
                "--name",
                "Reviewer",
            ],
            LaunchResponse);

        Assert.Equal(0, launched.ExitCode);
        Assert.Equal(0, resumed.ExitCode);
        var launch = launched.Request.Params!.Value;
        Assert.Equal("new", launch.GetProperty("mode").GetString());
        Assert.Equal("omp", launch.GetProperty("adapter").GetString());
        Assert.Equal("fast", launch.GetProperty("profile").GetString());
        Assert.Equal(
            "nook-a",
            launch.GetProperty("relativeToNookId").GetString());
        Assert.Equal("below", launch.GetProperty("placement").GetString());
        Assert.True(launch.GetProperty("yolo").GetBoolean());
        Assert.Equal(
            "openrouter/model-x",
            launch.GetProperty("model").GetString());
        Assert.Equal("high", launch.GetProperty("effort").GetString());
        Assert.Equal(
            "same-tab",
            launch.GetProperty("accessScope").GetString());
        var resume = resumed.Request.Params!.Value;
        Assert.Equal(
            "resume",
            resume.GetProperty("mode").GetString());
        Assert.Equal(
            "session-7",
            resume.GetProperty("sessionId").GetString());
        Assert.Equal(
            "Reviewer",
            resume.GetProperty("name").GetString());
        Assert.False(resume.TryGetProperty("profile", out _));
    }

    [Fact]
    public async Task AgentLaunch_OmittedSelectionsRemainNull()
    {
        var launched = await InvokeAsync(
            AgentCommands.AgentLaunch,
            ["pi", "--profile", "default"],
            LaunchResponse);

        Assert.Equal(0, launched.ExitCode);
        var parameters = launched.Request.Params!.Value.Deserialize(
            CoveJsonContext.Default.AgentLaunchParams)!;
        Assert.Null(parameters.Model);
        Assert.Null(parameters.Effort);
        Assert.Equal("default", parameters.Profile);
    }

    [Fact]
    public async Task NookRestart_MapsCommandAndFallbackParameters()
    {
        var result = await InvokeAsync(
            NookCommands.NookRestart,
            [
                "nook-a",
                "--mode",
                "command",
                "--command",
                "/bin/sh",
                "--arg",
                "-lc",
                "--arg",
                "exec fish",
                "--cwd",
                "/repo",
                "--resume-fallback",
                "fresh",
                "--no-preserve-scrollback",
            ],
            RestartResponse);

        Assert.Equal(0, result.ExitCode);
        var parameters = result.Request.Params!.Value;
        Assert.Equal(
            "nook-a",
            parameters.GetProperty("nookId").GetString());
        Assert.Equal(
            "command",
            parameters.GetProperty("mode").GetString());
        Assert.False(
            parameters.GetProperty("preserveScrollback")
                .GetBoolean());
        Assert.Equal(
            "/bin/sh",
            parameters.GetProperty("command").GetString());
        Assert.Equal(
            ["-lc", "exec fish"],
            parameters.GetProperty("args")
                .EnumerateArray()
                .Select(item => item.GetString()));
        Assert.Equal(
            "fresh",
            parameters.GetProperty("resumeFallback").GetString());
    }

    private static ControlResponse AgentListResponse(
        ControlRequest request) => new(
        request.Id,
        true,
        JsonSerializer.SerializeToElement(
            new AgentListResult(
            [
                new AgentListDto(
                    "nook-b",
                    "omp",
                    "Builder",
                    "bay-1",
                    "shore-1",
                    "working",
                    "same-tab"),
            ]),
            CoveJsonContext.Default.AgentListResult));

    private static ControlResponse LaunchResponse(
        ControlRequest request) => new(
        request.Id,
        true,
        JsonSerializer.SerializeToElement(
            new AgentLaunchResult(
                "nook-new",
                "omp",
                null,
                "bay-1",
                "shore-1",
                "right",
                false),
            CoveJsonContext.Default.AgentLaunchResult));

    private static ControlResponse RestartResponse(
        ControlRequest request) => new(
        request.Id,
        true,
        JsonSerializer.SerializeToElement(
            new NookRestartResult(
                "nook-a",
                "command",
                "command",
                false,
                null,
                null,
                "bay-1",
                "shore-1",
                0),
            CoveJsonContext.Default.NookRestartResult));

    private static string Golden(string name)
    {
        var directory = AppContext.BaseDirectory;
        for (var index = 0; index < 6; index++)
        {
            var path = Path.Combine(
                directory,
                "goldens",
                name);
            if (File.Exists(path))
                return File.ReadAllText(path);
            directory = Path.GetDirectoryName(directory)!;
        }
        throw new FileNotFoundException(name);
    }

    private static async Task<CommandResult> InvokeAsync(
        Func<CommandContext, Task<int>> invoke,
        string[] args,
        Func<ControlRequest, ControlResponse> respond)
    {
        var root = TestDirectory.Create(
            "agent-cli-",
            OperatingSystem.IsWindows() ? null : "/tmp");
        try
        {
            var paths = new DaemonPaths(
                CoveDataDir.ForRoot(CoveChannel.Stable, root));
            var endpoint =
                ControlEndpointFactory.FromSocketPath(
                    paths.DataDir.SocketPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    paths.DataDir.SocketPath)!);
            File.WriteAllText(
                paths.ControlTokenPath,
                "agent-control-cli-token");
            await using var listener = endpoint.Bind();
            using var cts = new CancellationTokenSource(
                TimeSpan.FromSeconds(15));
            var server = ServeAsync(
                listener,
                respond,
                cts.Token);
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
            var exitCode = await invoke(context);
            var request = await server;
            return new CommandResult(
                exitCode,
                request,
                stdout.ToString(),
                stderr.ToString());
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    private static async Task<ControlRequest> ServeAsync(
        IControlListener listener,
        Func<ControlRequest, ControlResponse> respond,
        CancellationToken cancellationToken)
    {
        await using var stream =
            await listener.AcceptAsync(cancellationToken);
        await using var connection =
            new FrameConnection(stream);
        var hello = (await connection.ReadFrameAsync(
            cancellationToken))!.Value;
        var helloRequest =
            ControlCodec.DecodeRequest(hello.Payload);
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
            ControlCodec.Encode(respond(request)),
            cancellationToken);
        return request;
    }

    private sealed record CommandResult(
        int ExitCode,
        ControlRequest Request,
        string Stdout,
        string Stderr);
}
