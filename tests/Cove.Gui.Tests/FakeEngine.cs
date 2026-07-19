using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Cove.Protocol;

public sealed class FakeEngine : IAsyncDisposable
{
    private static readonly AsyncLocal<ServeState?> CurrentServe = new();
    private readonly TcpListener _l = new(IPAddress.Loopback, 0);
    private readonly TaskCompletionSource _creditReaderPeerEof =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Port => ((IPEndPoint)_l.LocalEndpoint).Port;
    public readonly List<ulong> Credits = new();
    public readonly TaskCompletionSource<ulong> CreditReceived =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task CreditReaderPeerEof => _creditReaderPeerEof.Task;

    public FakeEngine() => _l.Start();

    public Func<CancellationToken, Task<Stream>> Dial => async ct =>
    {
        var c = new TcpClient();
        await c.ConnectAsync(IPAddress.Loopback, Port, ct);
        return c.GetStream();
    };

    public void CancelPendingConnections() => _l.Stop();

    public async Task ServeOnceAsync(
        ulong baseOffset,
        ulong replayUntilOffset,
        Func<Stream, Task> script,
        string terminalModePreambleBase64 = "",
        bool allowPeerEof = false)
    {
        using var conn = await _l.AcceptTcpClientAsync();
        var s = conn.GetStream();
        var hello = await Read(s);
        await WriteResponse(s, ReqId(hello), new HelloResult(1, "0.1.0", 1234, "dev"), CoveJsonContext.Default.HelloResult);
        var sub = await Read(s);
        await WriteResponse(s, ReqId(sub), new SubscribeResult(1, baseOffset, ProtocolConstants.FlowWindow, replayUntilOffset, terminalModePreambleBase64), CoveJsonContext.Default.SubscribeResult);
        using var readerCancellation = new CancellationTokenSource();
        var streamEndDelivered = allowPeerEof ? 1 : 0;
        var reader = ReadCreditsAsync(s, readerCancellation.Token);
        var previousServe = CurrentServe.Value;
        CurrentServe.Value = new ServeState(() => Volatile.Write(ref streamEndDelivered, 1));
        List<Exception>? failures = null;
        try
        {
            await script(s);
        }
        catch (Exception exception)
        {
            (failures ??= []).Add(exception);
        }

        CurrentServe.Value = previousServe;
        try
        {
            await readerCancellation.CancelAsync();
        }
        catch (Exception exception)
        {
            (failures ??= []).Add(exception);
        }

        EndOfStreamException? peerEof = null;
        try
        {
            peerEof = await reader;
        }
        catch (OperationCanceledException) when (readerCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            (failures ??= []).Add(exception);
        }

        if (peerEof is not null && Volatile.Read(ref streamEndDelivered) == 0)
            (failures ??= []).Add(peerEof);

        if (failures is null)
            return;
        if (failures.Count > 1)
            throw new AggregateException(failures);
        ExceptionDispatchInfo.Capture(failures[0]).Throw();
    }

    private async Task<EndOfStreamException?> ReadCreditsAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var frame = await Read(stream, cancellationToken);
                if (frame.type == FrameType.Credit)
                {
                    Credits.Add(BinaryPrimitives.ReadUInt64LittleEndian(frame.payload));
                    CreditReceived.TrySetResult(Credits[^1]);
                }
            }
        }
        catch (EndOfStreamException exception)
        {
            _creditReaderPeerEof.TrySetResult();
            return exception;
        }
    }

    public static async Task WriteStreamData(Stream s, ulong offset, byte[] raw)
    {
        CurrentServe.Value?.ThrowIfEnded("StreamData");
        var payload = new byte[8 + raw.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(payload, offset);
        raw.CopyTo(payload, 8);
        await WriteFrame(s, FrameType.StreamData, 1, payload);
    }
    public static Task WriteCompatibilityOnlyResync(Stream s, ulong newBase, string terminalModePreamble = "")
    {
        CurrentServe.Value?.ThrowIfEnded("compatibility-only Resync");
        var modes = System.Text.Encoding.ASCII.GetBytes(terminalModePreamble);
        var payload = new byte[8 + modes.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(payload, newBase);
        modes.CopyTo(payload, 8);
        return WriteFrame(s, FrameType.Resync, 1, payload);
    }
    public static Task WriteResync(Stream s, ulong newBase, string terminalModePreamble, byte[] terminalCheckpoint, int checkpointCols, int checkpointRows)
    {
        CurrentServe.Value?.ThrowIfEnded("Resync");
        var modes = System.Text.Encoding.ASCII.GetBytes(terminalModePreamble);
        var payload = new byte[20 + modes.Length + terminalCheckpoint.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(payload, newBase);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8), checkpointCols);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(12), checkpointRows);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(16), modes.Length);
        modes.CopyTo(payload, 20);
        terminalCheckpoint.CopyTo(payload, 20 + modes.Length);
        return WriteFrame(s, FrameType.Resync, 1, payload);
    }
    public static async Task WriteEnd(Stream s, ulong final, int code)
    {
        CurrentServe.Value?.ThrowIfEnded("StreamEnd");
        var p = new byte[12];
        BinaryPrimitives.WriteUInt64LittleEndian(p, final);
        BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(8), code);
        await WriteFrame(s, FrameType.StreamEnd, 1, p);
        CurrentServe.Value?.MarkEnded();
    }

    private static async Task WriteResponse<T>(Stream s, string id, T data, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> ti)
    {
        var el = JsonSerializer.SerializeToElement(data, ti);
        var resp = new ControlResponse(id, true, el);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(resp, CoveJsonContext.Default.ControlResponse);
        await WriteFrame(s, FrameType.Response, 0, bytes);
    }
    private static async Task WriteFrame(Stream s, FrameType type, ulong streamId, byte[] payload)
    {
        var buf = new byte[ProtocolConstants.HeaderSize + payload.Length];
        FrameHeader.Write(buf, new FrameHeader(type, streamId, 1, (uint)payload.Length));
        payload.CopyTo(buf, ProtocolConstants.HeaderSize);
        await s.WriteAsync(buf); await s.FlushAsync();
    }
    private static string ReqId((FrameType, byte[] payload) f) => JsonSerializer.Deserialize(f.Item2, CoveJsonContext.Default.ControlRequest)!.Id;
    private static async Task<(FrameType type, byte[] payload)> Read(Stream s, CancellationToken cancellationToken = default)
    {
        var h = new byte[ProtocolConstants.HeaderSize];
        await s.ReadExactlyAsync(h, cancellationToken);
        FrameHeader.TryRead(h, out var hd, out _);
        var p = new byte[hd.Length];
        if (hd.Length > 0) await s.ReadExactlyAsync(p, cancellationToken);
        return (hd.Type, p);
    }

    private sealed class ServeState(Action streamEndDelivered)
    {
        private int _streamEnded;

        public void MarkEnded()
        {
            Volatile.Write(ref _streamEnded, 1);
            streamEndDelivered();
        }

        public void ThrowIfEnded(string frameName)
        {
            if (Volatile.Read(ref _streamEnded) != 0)
                throw new InvalidOperationException($"Cannot write {frameName} after StreamEnd was delivered.");
        }
    }

    public async ValueTask DisposeAsync() { _l.Stop(); await Task.CompletedTask; }
}
