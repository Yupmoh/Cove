using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Cove.Protocol;
using Cove.Tui.Attach;
using Xunit;

namespace Cove.Tui.Tests;

public sealed class AttachCompositorTests
{
    [Fact]
    public async Task RunConnectedAsync_RendersIdleCheckpointBeforeStreamData()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (clientSocket, serverSocket) = await ConnectLoopbackAsync(timeout.Token);
        using (clientSocket)
        using (serverSocket)
        await using (var clientConnection = new FrameConnection(clientSocket.GetStream()))
        await using (var serverConnection = new FrameConnection(serverSocket.GetStream()))
        {
            var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var terminal = new TestAttachTerminal(text =>
            {
                if (text.Contains("idle-checkpoint", StringComparison.Ordinal))
                    rendered.TrySetResult();
            });
            var server = ServeUntilRenderedAsync(
                serverConnection,
                rendered.Task,
                Convert.ToBase64String(Encoding.UTF8.GetBytes("idle-checkpoint")),
                null,
                80,
                24,
                timeout.Token);

            var exitCode = await AttachCompositor.RunConnectedAsync(clientConnection, "dev", "nook-1", "user:tui", "test-control-token", terminal, timeout.Token);
            await server;

            Assert.Equal(0, exitCode);
            Assert.Contains("idle-checkpoint", terminal.OutputText);
        }
    }

    [Fact]
    public async Task RunConnectedAsync_AppliesModePreambleWithoutCheckpoint()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (clientSocket, serverSocket) = await ConnectLoopbackAsync(timeout.Token);
        using (clientSocket)
        using (serverSocket)
        await using (var clientConnection = new FrameConnection(clientSocket.GetStream()))
        await using (var serverConnection = new FrameConnection(serverSocket.GetStream()))
        {
            var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var terminal = new TestAttachTerminal(text =>
            {
                if (text.Contains("mode-only", StringComparison.Ordinal))
                    rendered.TrySetResult();
            });
            var server = ServeUntilRenderedAsync(
                serverConnection,
                rendered.Task,
                null,
                Convert.ToBase64String(Encoding.UTF8.GetBytes("mode-only")),
                80,
                24,
                timeout.Token);

            var exitCode = await AttachCompositor.RunConnectedAsync(clientConnection, "dev", "nook-1", "user:tui", "test-control-token", terminal, timeout.Token);
            await server;

            Assert.Equal(0, exitCode);
            Assert.Contains("mode-only", terminal.OutputText);
        }
    }

    [Fact]
    public async Task RunConnectedAsync_AppliesInitialModesBeforeCheckpointAtCheckpointDimensions()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (clientSocket, serverSocket) = await ConnectLoopbackAsync(timeout.Token);
        using (clientSocket)
        using (serverSocket)
        await using (var clientConnection = new FrameConnection(clientSocket.GetStream()))
        await using (var serverConnection = new FrameConnection(serverSocket.GetStream()))
        {
            var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var terminal = new TestAttachTerminal(text =>
            {
                if (text.Contains("5678", StringComparison.Ordinal))
                    rendered.TrySetResult();
            });
            var server = ServeUntilRenderedAsync(
                serverConnection,
                rendered.Task,
                Convert.ToBase64String(Encoding.UTF8.GetBytes("123456789")),
                Convert.ToBase64String(Encoding.UTF8.GetBytes("\x1b[?1049h")),
                4,
                2,
                timeout.Token);

            var exitCode = await AttachCompositor.RunConnectedAsync(clientConnection, "dev", "nook-1", "user:tui", "test-control-token", terminal, timeout.Token);
            await server;

            Assert.Equal(0, exitCode);
            Assert.Equal("\x1b[1;1H5678\x1b[2;1H9   ", terminal.OutputText);
        }
    }

    [Fact]
    public async Task RunConnectedAsync_AppliesMidStreamResyncModesCheckpointAndDimensions()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (clientSocket, serverSocket) = await ConnectLoopbackAsync(timeout.Token);
        using (clientSocket)
        using (serverSocket)
        await using (var clientConnection = new FrameConnection(clientSocket.GetStream()))
        await using (var serverConnection = new FrameConnection(serverSocket.GetStream()))
        {
            var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var terminal = new TestAttachTerminal(text =>
            {
                if (text.Contains("456", StringComparison.Ordinal))
                    rendered.TrySetResult();
            });
            var server = ServeResyncUntilRenderedAsync(
                serverConnection,
                rendered.Task,
                timeout.Token);

            var exitCode = await AttachCompositor.RunConnectedAsync(clientConnection, "dev", "nook-1", "user:tui", "test-control-token", terminal, timeout.Token);
            await server;

            Assert.Equal(0, exitCode);
            Assert.Equal(
                "\x1b[1;1HOLD\x1b[2J\x1b[H\x1b[1;1H456\x1b[2;1H7  ",
                terminal.OutputText);
        }
    }

    [Fact]
    public async Task RunConnectedAsync_DaemonEofCancelsInputRestoresTerminalAndUnhooksHandler()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (clientSocket, serverSocket) = await ConnectLoopbackAsync(timeout.Token);
        using (clientSocket)
        using (serverSocket)
        await using (var clientConnection = new FrameConnection(clientSocket.GetStream()))
        {
            var terminal = new TestAttachTerminal();
            var server = CompleteSubscriptionAndCloseAsync(serverSocket, timeout.Token);

            var exitCode = await AttachCompositor.RunConnectedAsync(clientConnection, "dev", "nook-1", "user:tui", "test-control-token", terminal, timeout.Token);
            await server;

            Assert.Equal(0, exitCode);
            Assert.True(terminal.CancelableInput!.ReadWasCanceled);
            Assert.Equal(1, terminal.AlternateScreenEnterCount);
            Assert.Equal(1, terminal.AlternateScreenExitCount);
            Assert.Equal(1, terminal.HandlerAddCount);
            Assert.Equal(1, terminal.HandlerRemoveCount);
            Assert.Equal(1, terminal.RawModeDisposeCount);
        }
    }

    [Fact]
    public async Task RunConnectedAsync_DaemonEofStopsCancellationIgnoringInputBeforeRestoringTerminal()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (clientSocket, serverSocket) = await ConnectLoopbackAsync(timeout.Token);
        using (clientSocket)
        using (serverSocket)
        using (var input = new CancellationIgnoringInputStream())
        await using (var clientConnection = new FrameConnection(clientSocket.GetStream()))
        {
            var inputStoppedBeforeRestore = false;
            var terminal = new TestAttachTerminal(
                input: input,
                onExit: () => inputStoppedBeforeRestore = input.ReadCompleted);
            var server = CompleteSubscriptionAndCloseAsync(serverSocket, timeout.Token);
            var elapsed = Stopwatch.StartNew();

            var exitCode = await AttachCompositor.RunConnectedAsync(clientConnection, "dev", "nook-1", "user:tui", "test-control-token", terminal, timeout.Token)
                .WaitAsync(TimeSpan.FromSeconds(3), timeout.Token);
            elapsed.Stop();
            await server;

            Assert.Equal(0, exitCode);
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(1));
            Assert.True(input.DisposeCalled);
            Assert.True(inputStoppedBeforeRestore);
            Assert.False(input.ReadActive);
            Assert.Equal("", terminal.ErrorText);
            Assert.Equal(1, terminal.AlternateScreenExitCount);
            Assert.Equal(1, terminal.RawModeDisposeCount);
        }
    }

    [Fact]
    public async Task RunConnectedAsync_PreservesPumpFailureAndReportsEveryCleanupFailure()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (clientSocket, serverSocket) = await ConnectLoopbackAsync(timeout.Token);
        using (clientSocket)
        using (serverSocket)
        await using (var clientConnection = new FrameConnection(clientSocket.GetStream()))
        {
            var terminal = new TestAttachTerminal(
                throwOnExit: true,
                throwOnRawDispose: true);
            var server = CompleteSubscriptionAndSendMalformedFrameAsync(serverSocket, timeout.Token);

            var exitCode = await AttachCompositor.RunConnectedAsync(clientConnection, "dev", "nook-1", "user:tui", "test-control-token", terminal, timeout.Token);
            await server;

            Assert.Equal(1, exitCode);
            Assert.Contains("malformed_frame", terminal.ErrorText);
            Assert.Contains("alternate-screen cleanup failed", terminal.ErrorText);
            Assert.Contains("raw-mode cleanup failed", terminal.ErrorText);
            Assert.Equal(1, terminal.HandlerRemoveCount);
        }
    }

    private static async Task ServeUntilRenderedAsync(
        FrameConnection connection,
        Task rendered,
        string? checkpoint,
        string? modes,
        int checkpointCols,
        int checkpointRows,
        CancellationToken ct)
    {
        await CompleteSubscriptionAsync(
            connection,
            checkpoint,
            modes,
            checkpointCols,
            checkpointRows,
            ct);
        await rendered.WaitAsync(TimeSpan.FromSeconds(2), ct);
        var end = new byte[StreamPayload.StreamEndSize];
        StreamPayload.WriteStreamEnd(end, 0, 0);
        await connection.WriteFrameAsync(FrameType.StreamEnd, 7, end, ct);
    }

    private static async Task ServeResyncUntilRenderedAsync(
        FrameConnection connection,
        Task rendered,
        CancellationToken ct)
    {
        await CompleteSubscriptionAsync(connection, null, null, 80, 24, ct);

        var initial = Encoding.UTF8.GetBytes("OLD");
        var data = new byte[StreamPayload.OffsetSize + initial.Length];
        var dataLength = StreamPayload.WriteStreamData(data, 0, initial);
        await connection.WriteFrameAsync(FrameType.StreamData, 7, data.AsMemory(0, dataLength), ct);

        var modes = Encoding.UTF8.GetBytes("\x1b[?1049h");
        var checkpoint = Encoding.UTF8.GetBytes("1234567");
        var resync = new byte[StreamPayload.ResyncHeaderSize + modes.Length + checkpoint.Length];
        var resyncLength = StreamPayload.WriteResync(resync, 3, 3, 2, modes, checkpoint);
        await connection.WriteFrameAsync(FrameType.Resync, 7, resync.AsMemory(0, resyncLength), ct);

        await rendered.WaitAsync(TimeSpan.FromSeconds(2), ct);
        var end = new byte[StreamPayload.StreamEndSize];
        StreamPayload.WriteStreamEnd(end, 3, 0);
        await connection.WriteFrameAsync(FrameType.StreamEnd, 7, end, ct);
    }

    private static async Task CompleteSubscriptionAndCloseAsync(TcpClient serverSocket, CancellationToken ct)
    {
        await using var connection = new FrameConnection(serverSocket.GetStream());
        await CompleteSubscriptionAsync(connection, null, null, 80, 24, ct);
        serverSocket.Client.Shutdown(SocketShutdown.Both);
        serverSocket.Close();
    }

    private static async Task CompleteSubscriptionAndSendMalformedFrameAsync(
        TcpClient serverSocket,
        CancellationToken ct)
    {
        var stream = serverSocket.GetStream();
        await using var connection = new FrameConnection(stream);
        await CompleteSubscriptionAsync(connection, null, null, 80, 24, ct);
        byte[] malformed =
        [
            0x4e, 0x4f, 0x50, 0x45, 0x01, 0x05, 0x00, 0x00,
            0x07, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        ];
        await stream.WriteAsync(malformed, ct);
        await stream.FlushAsync(ct);
    }

    private static async Task CompleteSubscriptionAsync(
        FrameConnection connection,
        string? checkpoint,
        string? modes,
        int checkpointCols,
        int checkpointRows,
        CancellationToken ct)
    {
        var helloFrame = (await connection.ReadFrameAsync(ct))!.Value;
        var hello = ControlCodec.DecodeRequest(helloFrame.Payload);
        await connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(new ControlResponse(hello.Id, true)),
            ct);

        var subscribeFrame = (await connection.ReadFrameAsync(ct))!.Value;
        var subscribe = ControlCodec.DecodeRequest(subscribeFrame.Payload);
        var result = JsonSerializer.SerializeToElement(
            new SubscribeResult(
                7,
                0,
                4096,
                0,
                modes ?? "",
                checkpoint ?? "",
                checkpointCols,
                checkpointRows),
            CoveJsonContext.Default.SubscribeResult);
        await connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(new ControlResponse(subscribe.Id, true, result)),
            ct);
    }

    private static async Task<(TcpClient Client, TcpClient Server)> ConnectLoopbackAsync(CancellationToken ct)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            var client = new TcpClient();
            var connect = client.ConnectAsync(endpoint.Address, endpoint.Port, ct);
            var server = await listener.AcceptTcpClientAsync(ct);
            await connect;
            return (client, server);
        }
        finally
        {
            listener.Stop();
        }
    }

    private sealed class TestAttachTerminal(
        Action<string>? onWrite = null,
        bool throwOnExit = false,
        bool throwOnRawDispose = false,
        Stream? input = null,
        Action? onExit = null) : IAttachTerminal
    {
        private readonly TrackingWriter _output = new(onWrite);
        private readonly Stream _input = input ?? new CancelableInputStream();

        Stream IAttachTerminal.Input => _input;
        public CancelableInputStream? CancelableInput => _input as CancelableInputStream;
        public TextWriter Output => _output;
        public TextWriter Error { get; } = new StringWriter();
        public int Width => 80;
        public int Height => 24;
        public string OutputText => _output.ToString();
        public string ErrorText => Error.ToString()!;
        public int AlternateScreenEnterCount { get; private set; }
        public int AlternateScreenExitCount { get; private set; }
        public int HandlerAddCount { get; private set; }
        public int HandlerRemoveCount { get; private set; }
        public int RawModeDisposeCount { get; private set; }

        public IDisposable? EnterRawMode() =>
            new CallbackDisposable(() =>
            {
                RawModeDisposeCount++;
                if (throwOnRawDispose)
                    throw new InvalidOperationException("raw-mode cleanup failed");
            });

        public IDisposable RegisterCancelHandler(Action cancel)
        {
            HandlerAddCount++;
            return new CallbackDisposable(() => HandlerRemoveCount++);
        }

        public void EnterAlternateScreen() => AlternateScreenEnterCount++;

        public void ExitAlternateScreen()
        {
            AlternateScreenExitCount++;
            onExit?.Invoke();
            if (throwOnExit)
                throw new InvalidOperationException("alternate-screen cleanup failed");
        }
    }

    private sealed class TrackingWriter(Action<string>? onWrite) : StringWriter
    {
        public override void Write(string? value)
        {
            base.Write(value);
            if (value is not null)
                onWrite?.Invoke(value);
        }
    }

    private sealed class CallbackDisposable(Action callback) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                callback();
        }
    }

    private sealed class CancelableInputStream : Stream
    {
        public bool ReadWasCanceled { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return 0;
            }
            catch (OperationCanceledException)
            {
                ReadWasCanceled = true;
                throw;
            }
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class CancellationIgnoringInputStream : Stream
    {
        private readonly TaskCompletionSource<int> _stopped =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool DisposeCalled { get; private set; }
        public bool ReadActive { get; private set; }
        public bool ReadCompleted { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ReadActive = true;
            try
            {
                return await _stopped.Task;
            }
            finally
            {
                ReadActive = false;
                ReadCompleted = true;
            }
        }

        protected override void Dispose(bool disposing)
        {
            DisposeCalled = true;
            _stopped.TrySetResult(0);
            base.Dispose(disposing);
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
