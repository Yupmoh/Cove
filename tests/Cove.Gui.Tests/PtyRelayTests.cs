using System.Net.WebSockets;
using System.Text;
using Cove.Gui;
using Cove.Gui.Tests;
using Xunit;

public class PtyRelayTests
{
    [Fact]
    public async Task Relay_Forwards_Base_Data_Ack_Credit_End()
    {
        await using var engine = new FakeEngine();
        using var temp = GuiTestDirectory.Create("cove-relay-");
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "index.html"), "<html>ok</html>");
        await using var server = new LoopbackServer(temp.Path, engine.Dial, "0.1.0", "dev", port: 0);
        server.Start();

        var dataObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serve = engine.ServeOnceAsync(baseOffset: 0, replayUntilOffset: 4, script: async s =>
        {
            await FakeEngine.WriteStreamData(s, 0, Encoding.ASCII.GetBytes("hi\r\n"));
            if (!await dataObserved.Task)
                return;
            Assert.Equal(4UL, await AwaitWithin(engine.CreditReceived.Task, "waiting for relay credit"));
            await FakeEngine.WriteEnd(s, 4, 0);
            await AwaitWithin(engine.CreditReaderPeerEof, "waiting for peer EOF after stream end");
        }, terminalModePreambleBase64: "G1s/MTA0OWg=");

        using var ws = new ClientWebSocket();
        try
        {
            await Connect(ws, new Uri($"ws://127.0.0.1:{server.Port}/pty?nook=p1&since=0"));
            var baseMsg = await ReceiveText(ws);
            Assert.Contains("\"t\":\"base\"", baseMsg);
            Assert.Contains("\"head\":4", baseMsg);
            Assert.Contains("\"modes\":\"G1s/MTA0OWg=\"", baseMsg);

            var data = await ReceiveBinary(ws);
            Assert.Equal(12, data.Length);
            Assert.Equal(0UL, System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(data));
            Assert.Equal("hi\r\n", Encoding.ASCII.GetString(data, 8, 4));

            await ws.SendAsync(Encoding.UTF8.GetBytes("{\"t\":\"ack\",\"off\":4}"), WebSocketMessageType.Text, true, CancellationToken.None);
            dataObserved.TrySetResult(true);

            var endMsg = await ReceiveText(ws);
            Assert.Contains("\"t\":\"end\"", endMsg);

            await serve;
            Assert.Contains(4UL, engine.Credits);
        }
        finally
        {
            dataObserved.TrySetResult(false);
            ws.Dispose();
            engine.CancelPendingConnections();
            await AwaitWithin(serve, "cleaning up the data relay");
        }
    }

    [Fact]
    public async Task Relay_Forwards_Compatibility_Resync_As_Text_And_Resets_Base()
    {
        await using var engine = new FakeEngine();
        using var temp = GuiTestDirectory.Create("cove-relay-");
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "index.html"), "<html>ok</html>");
        await using var server = new LoopbackServer(temp.Path, engine.Dial, "0.1.0", "dev", port: 0);
        server.Start();

        var resyncObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serve = engine.ServeOnceAsync(baseOffset: 0, replayUntilOffset: 0, script: async s =>
        {
            await FakeEngine.WriteCompatibilityOnlyResync(s, 9437184, "\x1b[?1006h");
            await resyncObserved.Task;
            await FakeEngine.WriteEnd(s, 9437184, 0);
        });

        using var ws = new ClientWebSocket();
        try
        {
            await Connect(ws, new Uri($"ws://127.0.0.1:{server.Port}/pty?nook=p1&since=0"));
            _ = await ReceiveText(ws);
            var resync = await ReceiveText(ws);
            Assert.Contains("\"t\":\"resync\"", resync);
            Assert.Contains("9437184", resync);
            Assert.Contains("\"modes\":\"G1s/MTAwNmg=\"", resync);
            resyncObserved.TrySetResult();
            await serve;
        }
        finally
        {
            resyncObserved.TrySetResult();
            ws.Dispose();
            engine.CancelPendingConnections();
            await AwaitWithin(serve, "cleaning up the resync relay");
        }
    }

    [Fact]
    public async Task Relay_Forwards_Checkpoint_Resync_Metadata()
    {
        await using var engine = new FakeEngine();
        using var temp = GuiTestDirectory.Create("cove-relay-");
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "index.html"), "<html>ok</html>");
        await using var server = new LoopbackServer(temp.Path, engine.Dial, "0.1.0", "dev", port: 0);
        server.Start();
        var checkpoint = Encoding.ASCII.GetBytes("STATE");
        var resyncObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serve = engine.ServeOnceAsync(baseOffset: 0, replayUntilOffset: 0, script: async s =>
        {
            await FakeEngine.WriteResync(s, 50000, "\x1b[?1006h", checkpoint, 132, 40);
            await resyncObserved.Task;
            await FakeEngine.WriteEnd(s, 50000, 0);
        });

        using var ws = new ClientWebSocket();
        try
        {
            await Connect(ws, new Uri($"ws://127.0.0.1:{server.Port}/pty?nook=p1&since=0"));
            _ = await ReceiveText(ws);
            var resync = await ReceiveText(ws);

            Assert.Contains("\"base\":50000", resync);
            Assert.Contains("\"modes\":\"G1s/MTAwNmg=\"", resync);
            Assert.Contains("\"checkpoint\":\"U1RBVEU=\"", resync);
            Assert.Contains("\"checkpointCols\":132", resync);
            Assert.Contains("\"checkpointRows\":40", resync);
            resyncObserved.TrySetResult();
            await serve;
        }
        finally
        {
            resyncObserved.TrySetResult();
            ws.Dispose();
            engine.CancelPendingConnections();
            await AwaitWithin(serve, "cleaning up the checkpoint resync relay");
        }
    }

    [Fact]
    public async Task Relay_Streams_Four_Terminals_Concurrently_Without_CrossTalk()
    {
        await using var engine = new FakeEngine();
        using var temp = GuiTestDirectory.Create("cove-relay-");
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "index.html"), "<html>ok</html>");
        await using var server = new LoopbackServer(temp.Path, engine.Dial, "0.1.0", "dev", port: 0);
        server.Start();

        var expected = Enumerable.Range(0, 4).Select(i => $"terminal-{i}\r\n").ToArray();
        var observed = expected.Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
        var serves = expected.Select((payload, index) => engine.ServeOnceAsync(0, (ulong)payload.Length, async stream =>
        {
            await FakeEngine.WriteStreamData(stream, 0, Encoding.ASCII.GetBytes(payload));
            await observed[index].Task;
            await FakeEngine.WriteEnd(stream, (ulong)payload.Length, 0);
        })).ToArray();
        var sockets = new List<ClientWebSocket>();
        try
        {
            for (var i = 0; i < expected.Length; i++)
            {
                var ws = new ClientWebSocket();
                sockets.Add(ws);
                await Connect(ws, new Uri($"ws://127.0.0.1:{server.Port}/pty?nook=p{i}&since=0"));
                _ = await ReceiveText(ws);
            }
            var received = new List<string>();
            for (var i = 0; i < sockets.Count; i++)
            {
                var ws = sockets[i];
                var frame = await ReceiveBinary(ws);
                Assert.Equal(0UL, System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(frame));
                received.Add(Encoding.ASCII.GetString(frame, 8, frame.Length - 8));
                observed[i].TrySetResult();
            }
            Assert.Equal(expected.Order(), received.Order());
            await Task.WhenAll(serves);
        }
        finally
        {
            foreach (var barrier in observed)
                barrier.TrySetResult();
            foreach (var ws in sockets) ws.Dispose();
            engine.CancelPendingConnections();
            await AwaitWithin(Task.WhenAll(serves), "cleaning up concurrent relays");
        }
    }

    [Theory]
    [InlineData(PostEndWrite.StreamData, "Cannot write StreamData after StreamEnd was delivered.")]
    [InlineData(PostEndWrite.Resync, "Cannot write Resync after StreamEnd was delivered.")]
    [InlineData(PostEndWrite.CompatibilityOnlyResync, "Cannot write compatibility-only Resync after StreamEnd was delivered.")]
    [InlineData(PostEndWrite.StreamEnd, "Cannot write StreamEnd after StreamEnd was delivered.")]
    public async Task Relay_Fake_Engine_Rejects_Writes_After_Stream_End(
        PostEndWrite postEndWrite,
        string expectedMessage)
    {
        await using var engine = new FakeEngine();
        using var temp = GuiTestDirectory.Create("cove-relay-");
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "index.html"), "<html>ok</html>");
        await using var server = new LoopbackServer(temp.Path, engine.Dial, "0.1.0", "dev", port: 0);
        server.Start();

        var serve = engine.ServeOnceAsync(0, 0, async stream =>
        {
            await FakeEngine.WriteEnd(stream, 0, 0);
            switch (postEndWrite)
            {
                case PostEndWrite.StreamData:
                    await FakeEngine.WriteStreamData(stream, 0, "late"u8.ToArray());
                    break;
                case PostEndWrite.Resync:
                    await FakeEngine.WriteResync(stream, 0, "", [], 80, 24);
                    break;
                case PostEndWrite.CompatibilityOnlyResync:
                    await FakeEngine.WriteCompatibilityOnlyResync(stream, 0);
                    break;
                case PostEndWrite.StreamEnd:
                    await FakeEngine.WriteEnd(stream, 0, 0);
                    break;
            }
        });
        using var ws = new ClientWebSocket();
        try
        {
            await Connect(ws, new Uri($"ws://127.0.0.1:{server.Port}/pty?nook=p1&since=0"));
            _ = await ReceiveText(ws);
            var end = await ReceiveText(ws);
            Assert.Contains("\"t\":\"end\"", end);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => AwaitWithin(serve, $"waiting for rejected post-end {postEndWrite}"));
            Assert.Equal(expectedMessage, exception.Message);
        }
        finally
        {
            ws.Dispose();
            engine.CancelPendingConnections();
            try
            {
                await AwaitWithin(serve, $"cleaning up rejected post-end {postEndWrite}");
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    [Fact]
    public async Task Relay_Peer_Eof_During_Failed_Stream_End_Write_Fails()
    {
        await using var engine = new FakeEngine();
        using var temp = GuiTestDirectory.Create("cove-relay-");
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "index.html"), "<html>ok</html>");
        await using var server = new LoopbackServer(temp.Path, engine.Dial, "0.1.0", "dev", port: 0);
        server.Start();

        var streamEndWriteStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStreamEndWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serve = engine.ServeOnceAsync(0, 0, async stream =>
        {
            using var failingStream = new BlockingFailedWriteStream(
                stream,
                streamEndWriteStarted,
                releaseStreamEndWrite);
            await FakeEngine.WriteEnd(failingStream, 0, 0);
        });
        using var ws = new ClientWebSocket();
        try
        {
            await Connect(ws, new Uri($"ws://127.0.0.1:{server.Port}/pty?nook=p1&since=0"));
            _ = await ReceiveText(ws);
            await AwaitWithin(streamEndWriteStarted.Task, "waiting for the stream-end write to start");

            ws.Abort();
            await AwaitWithin(engine.CreditReaderPeerEof, "waiting for peer EOF during the stream-end write");
            Assert.False(serve.IsCompleted, "The stream-end write must remain in progress until the injected write failure is released.");
            releaseStreamEndWrite.TrySetResult();

            var exception = await Assert.ThrowsAsync<AggregateException>(
                () => AwaitWithin(serve, "waiting for the failed stream-end write"));
            Assert.Collection(
                exception.InnerExceptions,
                static error => Assert.IsType<IOException>(error),
                static error => Assert.IsType<EndOfStreamException>(error));
        }
        finally
        {
            releaseStreamEndWrite.TrySetResult();
            ws.Dispose();
            engine.CancelPendingConnections();
        }
    }

    [Fact]
    public async Task Relay_Unexpected_Peer_Eof_Before_Stream_End_Fails()
    {
        await using var engine = new FakeEngine();
        using var temp = GuiTestDirectory.Create("cove-relay-");
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "index.html"), "<html>ok</html>");
        await using var server = new LoopbackServer(temp.Path, engine.Dial, "0.1.0", "dev", port: 0);
        server.Start();

        var releaseScript = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serve = engine.ServeOnceAsync(0, 0, _ => releaseScript.Task);
        using var ws = new ClientWebSocket();
        try
        {
            await Connect(ws, new Uri($"ws://127.0.0.1:{server.Port}/pty?nook=p1&since=0"));
            _ = await ReceiveText(ws);

            ws.Abort();
            await AwaitWithin(engine.CreditReaderPeerEof, "waiting for the credit reader to observe peer EOF");
            Assert.False(serve.IsCompleted, "The fake-engine script must still be active when operational EOF is observed.");
            releaseScript.TrySetResult();

            await Assert.ThrowsAsync<EndOfStreamException>(
                () => AwaitWithin(serve, "waiting for unexpected peer EOF to fail the fake engine"));
        }
        finally
        {
            releaseScript.TrySetResult();
            ws.Dispose();
            engine.CancelPendingConnections();
            try
            {
                await AwaitWithin(serve, "cleaning up the unexpected EOF relay");
            }
            catch (EndOfStreamException)
            {
            }
        }
    }

    private static async Task Connect(ClientWebSocket ws, Uri uri)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await ws.ConnectAsync(uri, cancellation.Token);
        }
        catch (OperationCanceledException exception) when (cancellation.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out connecting to relay WebSocket {uri}.", exception);
        }
    }

    public enum PostEndWrite
    {
        StreamData,
        Resync,
        CompatibilityOnlyResync,
        StreamEnd
    }

    private static async Task AwaitWithin(Task task, string operation)
    {
        try
        {
            await task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException($"Timed out {operation}.", exception);
        }
    }

    private static async Task<T> AwaitWithin<T>(Task<T> task, string operation)
    {
        try
        {
            return await task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException($"Timed out {operation}.", exception);
        }
    }

    private static async Task<string> ReceiveText(ClientWebSocket ws)
    {
        var buf = new byte[4096];
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            while (true)
            {
                var r = await ws.ReceiveAsync(buf, cancellation.Token);
                if (r.MessageType == WebSocketMessageType.Text) return Encoding.UTF8.GetString(buf, 0, r.Count);
            }
        }
        catch (OperationCanceledException exception) when (cancellation.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out waiting for a text relay message.", exception);
        }
    }

    private static async Task<byte[]> ReceiveBinary(ClientWebSocket ws)
    {
        var buf = new byte[4096];
        using var message = new MemoryStream();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            while (true)
            {
                var r = await ws.ReceiveAsync(buf, cancellation.Token);
                if (r.MessageType != WebSocketMessageType.Binary) continue;
                message.Write(buf, 0, r.Count);
                if (r.EndOfMessage) return message.ToArray();
            }
        }
        catch (OperationCanceledException exception) when (cancellation.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out waiting for a binary relay message.", exception);
        }
    }

    private sealed class BlockingFailedWriteStream(
        Stream inner,
        TaskCompletionSource writeStarted,
        TaskCompletionSource releaseWrite) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) =>
            inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) =>
            inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new IOException("Forced stream-end write failure.");
        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            writeStarted.TrySetResult();
            await releaseWrite.Task.WaitAsync(cancellationToken);
            throw new IOException("Forced stream-end write failure.");
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}
