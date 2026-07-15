using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Cove.Protocol;

public sealed class FakeEngine : IAsyncDisposable
{
    private readonly TcpListener _l = new(IPAddress.Loopback, 0);
    public int Port => ((IPEndPoint)_l.LocalEndpoint).Port;
    public readonly List<ulong> Credits = new();

    public FakeEngine() => _l.Start();

    public Func<CancellationToken, Task<Stream>> Dial => async ct =>
    {
        var c = new TcpClient();
        await c.ConnectAsync(IPAddress.Loopback, Port, ct);
        return c.GetStream();
    };

    public Task ServeOnceAsync(ulong baseOffset, ulong replayUntilOffset, Func<Stream, Task> script, string terminalModePreambleBase64 = "") => Task.Run(async () =>
    {
        using var conn = await _l.AcceptTcpClientAsync();
        var s = conn.GetStream();
        var hello = await Read(s);
        await WriteResponse(s, ReqId(hello), new HelloResult(1, "0.1.0", 1234, "dev"), CoveJsonContext.Default.HelloResult);
        var sub = await Read(s);
        await WriteResponse(s, ReqId(sub), new SubscribeResult(1, baseOffset, ProtocolConstants.FlowWindow, replayUntilOffset, terminalModePreambleBase64), CoveJsonContext.Default.SubscribeResult);
        _ = Task.Run(async () => { try { while (true) { var f = await Read(s); if (f.type == FrameType.Credit) Credits.Add(BinaryPrimitives.ReadUInt64LittleEndian(f.payload)); } } catch { Credits.TrimExcess(); } });
        await script(s);
    });

    public static async Task WriteStreamData(Stream s, ulong offset, byte[] raw)
    {
        var payload = new byte[8 + raw.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(payload, offset);
        raw.CopyTo(payload, 8);
        await WriteFrame(s, FrameType.StreamData, 1, payload);
    }
    public static Task WriteResync(Stream s, ulong newBase, string terminalModePreamble = "") { var modes = System.Text.Encoding.ASCII.GetBytes(terminalModePreamble); var p = new byte[8 + modes.Length]; BinaryPrimitives.WriteUInt64LittleEndian(p, newBase); modes.CopyTo(p, 8); return WriteFrame(s, FrameType.Resync, 1, p); }
    public static Task WriteCheckpointResync(Stream s, ulong newBase, string terminalModePreamble, byte[] terminalCheckpoint, int checkpointCols, int checkpointRows)
    {
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
    public static Task WriteEnd(Stream s, ulong final, int code) { var p = new byte[12]; BinaryPrimitives.WriteUInt64LittleEndian(p, final); BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(8), code); return WriteFrame(s, FrameType.StreamEnd, 1, p); }

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
    private static async Task<(FrameType type, byte[] payload)> Read(Stream s)
    {
        var h = new byte[ProtocolConstants.HeaderSize];
        await s.ReadExactlyAsync(h);
        FrameHeader.TryRead(h, out var hd, out _);
        var p = new byte[hd.Length];
        if (hd.Length > 0) await s.ReadExactlyAsync(p);
        return (hd.Type, p);
    }
    public async ValueTask DisposeAsync() { _l.Stop(); await Task.CompletedTask; }
}
