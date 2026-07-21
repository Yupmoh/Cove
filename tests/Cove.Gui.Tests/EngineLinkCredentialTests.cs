using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Cove.Protocol;
using Cove.Testing;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Cove.Gui.Tests;

public sealed class EngineLinkCredentialTests
{
    [Fact]
    public async Task Missing_control_token_is_logged_as_missing()
    {
        using var directory = GuiTestDirectory.Create("cove-gui-token-missing-");
        await using var environment = await ProcessEnvironmentScope.SetAsync("COVE_DATA_DIR", directory.Path);
        var logger = new CapturingLogger();

        HelloParams hello = await ConnectAndCaptureHelloAsync(logger);

        Assert.Null(hello.ControlToken);
        Assert.Contains(logger.Messages, message => message.Contains("control token missing", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("control token read failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Invalid_control_token_is_logged_as_read_failure()
    {
        using var directory = GuiTestDirectory.Create("cove-gui-token-invalid-");
        Directory.CreateDirectory(Path.Combine(directory.Path, "ipc"));
        File.WriteAllText(Path.Combine(directory.Path, "ipc", "dev.control-token"), "");
        await using var environment = await ProcessEnvironmentScope.SetAsync("COVE_DATA_DIR", directory.Path);
        var logger = new CapturingLogger();

        HelloParams hello = await ConnectAndCaptureHelloAsync(logger);

        Assert.Null(hello.ControlToken);
        Assert.Contains(logger.Messages, message => message.Contains("control token read failed", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("control token missing", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Mismatched_engine_version_is_rejected_before_application_request()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;

        try
        {
            Task server = ServeMismatchedVersionAsync(listener, cancellation.Token);
            await using var link = new EngineLink(
                async ct =>
                {
                    var client = new TcpClient();
                    await client.ConnectAsync(endpoint.Address, endpoint.Port, ct);
                    return client.GetStream();
                },
                "0.5.2",
                "stable",
                "test-token");

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => link.RequestAsync("cove://sys/ping", null, cancellation.Token));

            Assert.Contains("engine version mismatch", error.Message);
            await server;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<HelloParams> ConnectAndCaptureHelloAsync(ILogger logger)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;

        try
        {
            var captured = new TaskCompletionSource<HelloParams>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Task server = ServeAsync(listener, captured, cancellation.Token);
            await using var link = new EngineLink(
                async ct =>
                {
                    var client = new TcpClient();
                    await client.ConnectAsync(endpoint.Address, endpoint.Port, ct);
                    return client.GetStream();
                },
                "0.4.0",
                "dev");
            link.SetLogger(logger);

            ControlResponse response = await link.RequestAsync("cove://sys/ping", null, cancellation.Token);
            Assert.True(response.Ok);
            await server;
            return await captured.Task;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task ServeAsync(
        TcpListener listener,
        TaskCompletionSource<HelloParams> captured,
        CancellationToken cancellationToken)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var connection = new FrameConnection(client.GetStream());

        Frame helloFrame = (await connection.ReadFrameAsync(cancellationToken))!.Value;
        ControlRequest helloRequest = ControlCodec.DecodeRequest(helloFrame.Payload);
        HelloParams hello = JsonSerializer.Deserialize(
            helloRequest.Params!.Value,
            CoveJsonContext.Default.HelloParams)!;
        captured.SetResult(hello);
        JsonElement helloResult = JsonSerializer.SerializeToElement(
            new HelloResult(ProtocolConstants.SemanticProtocolVersion, "0.4.0", 1, "dev"),
            CoveJsonContext.Default.HelloResult);
        await connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(new ControlResponse(helloRequest.Id, true, helloResult)),
            cancellationToken);

        Frame pingFrame = (await connection.ReadFrameAsync(cancellationToken))!.Value;
        ControlRequest pingRequest = ControlCodec.DecodeRequest(pingFrame.Payload);
        await connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(new ControlResponse(pingRequest.Id, true)),
            cancellationToken);
    }

    private static async Task ServeMismatchedVersionAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var connection = new FrameConnection(client.GetStream());

        Frame helloFrame = (await connection.ReadFrameAsync(cancellationToken))!.Value;
        ControlRequest helloRequest = ControlCodec.DecodeRequest(helloFrame.Payload);
        JsonElement helloResult = JsonSerializer.SerializeToElement(
            new HelloResult(ProtocolConstants.SemanticProtocolVersion, "0.5.1", 1, "stable"),
            CoveJsonContext.Default.HelloResult);
        await connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(new ControlResponse(helloRequest.Id, true, helloResult)),
            cancellationToken);

        Frame? nextFrame = await connection.ReadFrameAsync(cancellationToken);
        Assert.Null(nextFrame);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();
            public void Dispose() { }
        }
    }
}
