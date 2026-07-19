using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using Cove.Gui;
using Cove.Gui.Tests;
using Cove.Protocol;
using Xunit;

public sealed class PtyRelayLifecycleTests
{
    [Fact]
    public async Task RejectedSubscriptionDisposesDialedStream()
    {
        await using var engine = new RejectingSubscriptionEngine();
        var streamDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serve = engine.ServeOnceAsync();

        async Task<Stream> Dial(CancellationToken ct)
            => new DisposeTrackingStream(await engine.Dial(ct), streamDisposed);

        await Assert.ThrowsAsync<InvalidOperationException>(() => PtyStreamClient.SubscribeAsync(
            Dial, "0.1.0", "dev", "missing", 0, CancellationToken.None));
        await streamDisposed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await serve;
    }

    [Fact]
    public async Task BrowserRelayEndingStopsEngineRelayAndDisposesStream()
    {
        await using var engine = new FakeEngine();
        var streamDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseEngine = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var temp = GuiTestDirectory.Create("cove-relay-lifecycle-");
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "index.html"), "<html>ok</html>");

        async Task<Stream> Dial(CancellationToken ct)
            => new DisposeTrackingStream(await engine.Dial(ct), streamDisposed);

        await using var server = new LoopbackServer(temp.Path, Dial, "0.1.0", "dev", port: 0);
        server.Start();
        var serve = engine.ServeOnceAsync(0, 0, _ => releaseEngine.Task, allowPeerEof: true);
        using var ws = new ClientWebSocket();
        try
        {
            await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{server.Port}/pty?nook=p1&since=0"), CancellationToken.None);
            _ = await ReceiveTextAsync(ws);

            ws.Abort();

            await streamDisposed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            ws.Abort();
            releaseEngine.TrySetResult();
            if (ws.State == WebSocketState.None)
                engine.CancelPendingConnections();
            await serve.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private static async Task<string> ReceiveTextAsync(ClientWebSocket ws)
    {
        var buffer = new byte[4096];
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            var result = await ws.ReceiveAsync(buffer, cancellation.Token);
            if (result.MessageType == WebSocketMessageType.Text)
                return System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
        }
    }

    private sealed class RejectingSubscriptionEngine : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

        public RejectingSubscriptionEngine() => _listener.Start();

        public async Task<Stream> Dial(CancellationToken ct)
        {
            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)_listener.LocalEndpoint).Port, ct);
            return client.GetStream();
        }

        public Task ServeOnceAsync() => Task.Run(async () =>
        {
            using var connection = await _listener.AcceptTcpClientAsync();
            var stream = connection.GetStream();
            var hello = await ReadRequestAsync(stream);
            var helloData = JsonSerializer.SerializeToElement(
                new HelloResult(ProtocolConstants.SemanticProtocolVersion, "0.1.0", 1234, "dev"),
                CoveJsonContext.Default.HelloResult);
            await WriteResponseAsync(stream, new ControlResponse(hello.Id, true, helloData));
            var subscribe = await ReadRequestAsync(stream);
            await WriteResponseAsync(stream, new ControlResponse(
                subscribe.Id, false, null, new ControlError("not_found", "nook not found")));
        });

        private static async Task<ControlRequest> ReadRequestAsync(Stream stream)
        {
            var headerBytes = new byte[ProtocolConstants.HeaderSize];
            await stream.ReadExactlyAsync(headerBytes);
            Assert.True(FrameHeader.TryRead(headerBytes, out var header, out var error), error);
            var payload = new byte[header.Length];
            if (payload.Length > 0)
                await stream.ReadExactlyAsync(payload);
            return JsonSerializer.Deserialize(payload, CoveJsonContext.Default.ControlRequest)!;
        }

        private static async Task WriteResponseAsync(Stream stream, ControlResponse response)
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(response, CoveJsonContext.Default.ControlResponse);
            var frame = new byte[ProtocolConstants.HeaderSize + payload.Length];
            FrameHeader.Write(frame, new FrameHeader(FrameType.Response, 0, 1, (uint)payload.Length));
            payload.CopyTo(frame, ProtocolConstants.HeaderSize);
            await stream.WriteAsync(frame);
            await stream.FlushAsync();
        }

        public ValueTask DisposeAsync()
        {
            _listener.Stop();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DisposeTrackingStream(Stream inner, TaskCompletionSource disposed) : Stream
    {
        private int _disposed;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => inner.WriteAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                try { inner.Dispose(); }
                finally { disposed.TrySetResult(); }
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                try { await inner.DisposeAsync(); }
                finally { disposed.TrySetResult(); }
            }
            GC.SuppressFinalize(this);
        }
    }
}
