using System.Net.Sockets;
using System.Text.Json;
using Cove.Engine.Control;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ControlClientTests
{
    [Fact]
    public void SerializeEnvelope_Req_RoundTrips()
    {
        var envelope = new ControlEnvelope("req", 1, "nook.split", null, null, null, null, null, null, null);
        var json = JsonSerializer.SerializeToUtf8Bytes(envelope, ControlJsonContext.Default.ControlEnvelope);
        var decoded = JsonSerializer.Deserialize(json, ControlJsonContext.Default.ControlEnvelope);

        Assert.NotNull(decoded);
        Assert.Equal("req", decoded!.Kind);
        Assert.Equal(1, decoded.Id);
        Assert.Equal("nook.split", decoded.Method);
    }

    [Fact]
    public void SerializeEnvelope_Res_WithResult_RoundTrips()
    {
        using var doc = JsonDocument.Parse("""{"nookId":"p-42"}""");
        var envelope = new ControlEnvelope("res", 1, null, null, doc.RootElement.Clone(), null, null, null, null, null);
        var json = JsonSerializer.SerializeToUtf8Bytes(envelope, ControlJsonContext.Default.ControlEnvelope);
        var decoded = JsonSerializer.Deserialize(json, ControlJsonContext.Default.ControlEnvelope);

        Assert.NotNull(decoded);
        Assert.Equal("res", decoded!.Kind);
        Assert.Equal("p-42", decoded.Result!.Value.GetProperty("nookId").GetString());
    }

    [Fact]
    public void SerializeEnvelope_Evt_RoundTrips()
    {
        using var doc = JsonDocument.Parse("""{"shore":"dev"}""");
        var envelope = new ControlEnvelope("evt", null, "shore.changed", doc.RootElement.Clone(), null, null, null, null, null, null);
        var json = JsonSerializer.SerializeToUtf8Bytes(envelope, ControlJsonContext.Default.ControlEnvelope);
        var decoded = JsonSerializer.Deserialize(json, ControlJsonContext.Default.ControlEnvelope);

        Assert.NotNull(decoded);
        Assert.Equal("evt", decoded!.Kind);
        Assert.Equal("shore.changed", decoded.Method);
        Assert.Equal("dev", decoded.Params!.Value.GetProperty("shore").GetString());
    }

    [Fact]
    public void SerializeEnvelope_Pty_RoundTrips()
    {
        var envelope = new ControlEnvelope("pty", null, null, null, null, null, null, "nook-1", 1024L, 0);
        var json = JsonSerializer.SerializeToUtf8Bytes(envelope, ControlJsonContext.Default.ControlEnvelope);
        var decoded = JsonSerializer.Deserialize(json, ControlJsonContext.Default.ControlEnvelope);

        Assert.NotNull(decoded);
        Assert.Equal("pty", decoded!.Kind);
        Assert.Equal("nook-1", decoded.NookId);
        Assert.Equal(1024L, decoded.Offset);
    }

    [Fact]
    public async Task ConnectAndInvoke_StubDaemon_ReturnsResult()
    {
        await using var stub = new StubDaemon();
        await stub.StartAsync();
        await using var client = new ControlClient(stub.Port.ToString());

        await client.ConnectAsync();
        var result = await client.SendAsync("shore.list", null);

        Assert.Equal("ok", result.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Subscribe_StubDaemon_ReceivesEvent()
    {
        await using var stub = new StubDaemon();
        await stub.StartAsync();
        await using var client = new ControlClient(stub.Port.ToString());

        await client.ConnectAsync();
        var events = client.SubscribeAsync("shore.changed");
        await stub.SendEventAsync("shore.changed", """{"shore":"dev"}""");

        var evt = await events.GetAsyncEnumerator().MoveNextAsync();
        Assert.True(evt);
    }

    [Fact]
    public async Task StreamPty_StubDaemon_ReceivesRawBytes()
    {
        await using var stub = new StubDaemon();
        await stub.StartAsync();
        await using var client = new ControlClient(stub.Port.ToString());

        await client.ConnectAsync();
        var ptyStream = client.StreamPtyAsync("nook-1", 0);
        var expectedBytes = System.Text.Encoding.UTF8.GetBytes("hello pty world");

        await stub.SendPtyDataAsync("nook-1", 0, expectedBytes);

        var enumerator = ptyStream.GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(expectedBytes, enumerator.Current.Data);
        Assert.Equal("nook-1", enumerator.Current.NookId);
    }

    [Fact]
    public async Task SendPtyInput_StubDaemon_ReceivesBytes()
    {
        await using var stub = new StubDaemon();
        await stub.StartAsync();
        await using var client = new ControlClient(stub.Port.ToString());

        await client.ConnectAsync();
        var inputBytes = System.Text.Encoding.UTF8.GetBytes("ls -la\r");
        await client.SendPtyInputAsync("nook-1", inputBytes);

        var received = await stub.WaitForPtyInputAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(inputBytes, received);
    }

    [Fact]
    public async Task ConcurrentSends_StubDaemon_FramesNeverInterleave()
    {
        await using var stub = new StubDaemon();
        await stub.StartAsync();
        await using var client = new ControlClient(stub.Port.ToString());

        await client.ConnectAsync();

        const int count = 200;
        var expected = new HashSet<string>();
        var inputs = new List<byte[]>();
        for (var i = 0; i < count; i++)
        {
            var payload = System.Text.Encoding.UTF8.GetBytes($"input-{i:D4}-payload-block");
            inputs.Add(payload);
            expected.Add(System.Convert.ToBase64String(payload));
        }

        var tasks = new List<Task>();
        for (var i = 0; i < count; i++)
        {
            var payload = inputs[i];
            tasks.Add(Task.Run(() => client.SendPtyInputAsync("nook-1", payload)));
            tasks.Add(Task.Run(() => client.SendAsync("shore.list", null)));
        }
        await Task.WhenAll(tasks);

        var received = new HashSet<string>();
        for (var i = 0; i < count; i++)
        {
            var bytes = await stub.WaitForPtyInputAsync(TimeSpan.FromSeconds(10));
            received.Add(System.Convert.ToBase64String(bytes));
        }

        Assert.Equal(expected, received);
    }
}

internal sealed class StubDaemon : IAsyncDisposable
{
    private TcpListener? _listener;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private Task? _acceptTask;
    private Task? _handleTask;
    private readonly System.Threading.CancellationTokenSource _cts = new();
    private readonly System.Threading.Channels.Channel<byte[]> _ptyInputChannel =
        System.Threading.Channels.Channel.CreateUnbounded<byte[]>();

    public int Port { get; private set; }

    public async Task StartAsync()
    {
        _listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((System.Net.IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptTask = AcceptAndHandleAsync();
        await Task.Yield();
    }

    private async Task AcceptAndHandleAsync()
    {
        try
        {
            _client = await _listener!.AcceptTcpClientAsync(_cts.Token);
            _stream = _client.GetStream();
            _handleTask = HandleClientAsync();
        }
        catch (OperationCanceledException) { }
    }

    private async Task HandleClientAsync()
    {
        var lengthBuffer = new byte[4];
        try
        {
            while (!_cts.IsCancellationRequested && _stream is not null)
            {
                var read = await _stream.ReadAsync(lengthBuffer, _cts.Token);
                if (read == 0) break;
                if (read < 4) continue;

                var totalLength = System.BitConverter.ToInt32(lengthBuffer);
                if (totalLength <= 0 || totalLength > 10 * 1024 * 1024) continue;

                var buffer = new byte[totalLength];
                var totalRead = 0;
                while (totalRead < totalLength)
                {
                    var r = await _stream.ReadAsync(buffer.AsMemory(totalRead), _cts.Token);
                    if (r == 0) break;
                    totalRead += r;
                }

                var reader = new Utf8JsonReader(buffer.AsSpan(0, totalRead));
                var envelope = JsonSerializer.Deserialize(ref reader, ControlJsonContext.Default.ControlEnvelope);
                if (envelope is null) continue;

                var jsonBytes = (int)reader.BytesConsumed;
                var binaryData = jsonBytes < totalRead ? buffer[jsonBytes..totalRead] : null;

                await HandleEnvelopeAsync(envelope, binaryData);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task HandleEnvelopeAsync(ControlEnvelope envelope, byte[]? binaryData)
    {
        switch (envelope.Kind)
        {
            case "req":
                var resultJson = """{"status":"ok"}""";
                using (var doc = JsonDocument.Parse(resultJson))
                {
                    var res = new ControlEnvelope("res", envelope.Id, null, null, doc.RootElement.Clone(), null, null, null, null, null);
                    await WriteFrameAsync(res, null);
                }
                break;
            case "sub":
                break;
            case "pty-in":
                if (binaryData is not null)
                    await _ptyInputChannel.Writer.WriteAsync(binaryData);
                break;
        }
    }

    public async Task SendEventAsync(string topic, string paramsJson)
    {
        using var doc = JsonDocument.Parse(paramsJson);
        var envelope = new ControlEnvelope("evt", null, topic, doc.RootElement.Clone(), null, null, null, null, null, null);
        await WriteFrameAsync(envelope, null);
    }

    public async Task SendPtyDataAsync(string nookId, long offset, byte[] data)
    {
        var envelope = new ControlEnvelope("pty", null, null, null, null, null, null, nookId, offset, data.Length);
        await WriteFrameAsync(envelope, data);
    }

    private async Task WriteFrameAsync(ControlEnvelope envelope, byte[]? binary)
    {
        if (_stream is null) return;
        var json = JsonSerializer.SerializeToUtf8Bytes(envelope, ControlJsonContext.Default.ControlEnvelope);
        var totalLength = json.Length + (binary?.Length ?? 0);
        var lengthPrefix = System.BitConverter.GetBytes(totalLength);

        await _stream.WriteAsync(lengthPrefix);
        await _stream.WriteAsync(json);
        if (binary is not null)
            await _stream.WriteAsync(binary);
    }

    public async Task<byte[]> WaitForPtyInputAsync(System.TimeSpan timeout)
    {
        using var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        cts.CancelAfter(timeout);
        try
        {
            return await _ptyInputChannel.Reader.ReadAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new System.TimeoutException("no pty-in received within timeout");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _stream?.Dispose();
        _client?.Dispose();
        if (_listener is not null)
            _listener.Stop();
        _cts.Dispose();
        _ptyInputChannel.Writer.TryComplete();
        if (_acceptTask is not null) await _acceptTask.ConfigureAwait(false);
        if (_handleTask is not null) await _handleTask.ConfigureAwait(false);
    }
}
