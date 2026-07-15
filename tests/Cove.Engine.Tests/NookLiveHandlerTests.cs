using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NookLiveHandlerTests
{
    private static async Task<ControlResponse> RequestAsync(
        FrameConnection conn, string id, string uri, JsonElement? p, CancellationToken ct)
    {
        await conn.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest(id, uri, p)), ct);
        while (true)
        {
            Frame f = (await conn.ReadFrameAsync(ct))!.Value;
            if (f.Header.Type != FrameType.Response)
                continue;
            ControlResponse r = ControlCodec.DecodeResponse(f.Payload);
            if (r.Id == id)
                return r;
        }
    }

    private static async Task<string> SpawnAsync(
        FrameConnection conn, string command, string[] args, int cols, int rows, CancellationToken ct)
    {
        JsonElement p = JsonSerializer.SerializeToElement(
            new SpawnParams(command, args, null, null, cols, rows), CoveJsonContext.Default.SpawnParams);
        ControlResponse r = await RequestAsync(conn, "sp", "cove://commands/nook.spawn", p, ct);
        Assert.True(r.Ok, r.Error?.Message);
        NookInfo info = r.Data!.Value.Deserialize(CoveJsonContext.Default.NookInfo)!;
        return info.NookId;
    }

    private static async Task<NookInfo[]> ListAsync(FrameConnection conn, CancellationToken ct)
    {
        ControlResponse r = await RequestAsync(conn, "pl", "cove://commands/nook.list", null, ct);
        Assert.True(r.Ok);
        return r.Data!.Value.Deserialize(CoveJsonContext.Default.NookListResult)!.Nooks;
    }

    private static async Task<SubscribeResult> SubscribeAsync(
        FrameConnection conn, string nookId, ulong since, CancellationToken ct)
    {
        JsonElement p = JsonSerializer.SerializeToElement(
            new SubscribeParams(nookId, since), CoveJsonContext.Default.SubscribeParams);
        ControlResponse r = await RequestAsync(conn, "su", "cove://commands/nook.subscribe", p, ct);
        Assert.True(r.Ok, r.Error?.Message);
        return r.Data!.Value.Deserialize(CoveJsonContext.Default.SubscribeResult)!;
    }

    private static async Task<byte[]> ReadRawAsync(
        FrameConnection conn, ulong streamId, int count, CancellationToken ct)
    {
        var acc = new MemoryStream();
        while (acc.Length < count)
        {
            Frame f = (await conn.ReadFrameAsync(ct))!.Value;
            if (f.Header.Type != FrameType.StreamData || f.Header.StreamId != streamId)
                continue;
            acc.Write(f.Payload, 8, f.Payload.Length - 8);
        }
        byte[] all = acc.ToArray();
        byte[] head = new byte[count];
        Array.Copy(all, head, count);
        return head;
    }

    private static JsonElement WriteParamsJson(string nookId, string dataBase64)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("nookId", nookId);
            w.WriteString("dataBase64", dataBase64);
            w.WriteEndObject();
        }
        using var doc = JsonDocument.Parse(ms.ToArray());
        return doc.RootElement.Clone();
    }

    private static JsonElement NookRefJson(string nookId)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("nookId", nookId);
            w.WriteEndObject();
        }
        using var doc = JsonDocument.Parse(ms.ToArray());
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task RunningNook_SurvivesClientClose_AndReplaysRingIdenticallyOnReattach()
    {
        if (OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        const string marker = "COVE_REATTACH_PROOF";
        string nookId = await SpawnAsync(
            ctl, "/bin/sh", new[] { "-c", "echo " + marker + "; sleep 30" }, 80, 24, ct);

        byte[] first;
        {
            await using FrameConnection a = await h.ConnectAsync("gui");
            SubscribeResult sub = await SubscribeAsync(a, nookId, 0, ct);
            first = await ReadRawAsync(a, sub.StreamId, marker.Length, ct);
        }

        NookInfo[] afterClose = await ListAsync(ctl, ct);
        Assert.Contains(afterClose, p => p.NookId == nookId && p.Alive);

        byte[] second;
        {
            await using FrameConnection b = await h.ConnectAsync("gui");
            SubscribeResult sub = await SubscribeAsync(b, nookId, 0, ct);
            second = await ReadRawAsync(b, sub.StreamId, marker.Length, ct);
        }

        Assert.Equal(marker, Encoding.ASCII.GetString(first));
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task NookSpawn_ListsAlive_ThenKill_Removes()
    {
        if (OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string nookId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "sleep 30" }, 100, 40, ct);

        NookInfo[] listed = await ListAsync(ctl, ct);
        NookInfo nook = Assert.Single(listed, p => p.NookId == nookId);
        Assert.True(nook.Alive);
        Assert.Equal(100, nook.Cols);
        Assert.Equal(40, nook.Rows);

        ControlResponse kill = await RequestAsync(ctl, "kl", "cove://commands/nook.kill", NookRefJson(nookId), ct);
        Assert.True(kill.Ok, kill.Error?.Message);

        NookInfo[] after = await ListAsync(ctl, ct);
        Assert.DoesNotContain(after, p => p.NookId == nookId && p.Alive);

        ControlResponse missing = await RequestAsync(ctl, "kl2", "cove://commands/nook.kill", NookRefJson("nook-does-not-exist"), ct);
        Assert.False(missing.Ok);
        Assert.Equal("not_found", missing.Error!.Code);
    }

    [Fact]
    public async Task NookWrite_ReachesChild_AndOutputStreams()
    {
        if (OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string nookId = await SpawnAsync(ctl, "/bin/cat", Array.Empty<string>(), 80, 24, ct);
        await using FrameConnection sink = await h.ConnectAsync("gui");
        SubscribeResult sub = await SubscribeAsync(sink, nookId, 0, ct);

        const string ping = "COVE_PING\n";
        string b64 = Convert.ToBase64String(Encoding.ASCII.GetBytes(ping));
        ControlResponse w = await RequestAsync(ctl, "wr", "cove://commands/nook.write", WriteParamsJson(nookId, b64), ct);
        Assert.True(w.Ok, w.Error?.Message);

        byte[] echoed = await ReadRawAsync(sink, sub.StreamId, "COVE_PING".Length, ct);
        Assert.Contains("COVE_PING", Encoding.ASCII.GetString(echoed) + "COVE_PING");
        Assert.Equal("COVE_PING", Encoding.ASCII.GetString(echoed));
    }

    [Fact]
    public async Task NookResize_LiveNook_Ok_MissingNook_NotFound()
    {
        if (OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string nookId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "sleep 30" }, 80, 24, ct);
        JsonElement rp = JsonSerializer.SerializeToElement(
            new ResizeParams(nookId, 120, 50), CoveJsonContext.Default.ResizeParams);
        ControlResponse ok = await RequestAsync(ctl, "rs", "cove://commands/nook.resize", rp, ct);
        Assert.True(ok.Ok, ok.Error?.Message);

        JsonElement rpMissing = JsonSerializer.SerializeToElement(
            new ResizeParams("nook-nope", 120, 50), CoveJsonContext.Default.ResizeParams);
        ControlResponse missing = await RequestAsync(ctl, "rs2", "cove://commands/nook.resize", rpMissing, ct);
        Assert.False(missing.Ok);
        Assert.Equal("not_found", missing.Error!.Code);
    }

    [Fact]
    public async Task Subscribe_FromCheckpointOffset_ReturnsSerializedStateAndDimensions()
    {
        if (OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");
        string nookId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "printf '\\033[?1049h\\033[?1006h'; sleep 30" }, 80, 24, ct);
        await Task.Delay(200, ct);
        byte[] checkpoint = Encoding.UTF8.GetBytes("SERIALIZED_STATE");
        var checkpointParams = new NookCheckpointParams(nookId, Convert.ToBase64String(checkpoint), 1, 132, 40, 10000);
        JsonElement checkpointElement = JsonSerializer.SerializeToElement(checkpointParams, CoveJsonContext.Default.NookCheckpointParams);
        ControlResponse stored = await RequestAsync(ctl, "cp", "cove://commands/nook.checkpoint", checkpointElement, ct);
        Assert.True(stored.Ok, stored.Error?.Message);

        await using FrameConnection gui = await h.ConnectAsync("gui");
        SubscribeResult result = await SubscribeAsync(gui, nookId, 0, ct);

        Assert.Equal(Convert.ToBase64String(checkpoint), result.TerminalCheckpointBase64);
        Assert.Equal(132, result.CheckpointCols);
        Assert.Equal(40, result.CheckpointRows);
        Assert.Equal(Convert.ToBase64String(Encoding.ASCII.GetBytes("\x1b[?1006h")), result.TerminalModePreambleBase64);

        await using FrameConnection reconnect = await h.ConnectAsync("gui");
        SubscribeResult continued = await SubscribeAsync(reconnect, nookId, 1, ct);
        Assert.Equal(1ul, continued.BaseOffset);
        Assert.Equal("", continued.TerminalCheckpointBase64);
    }

    [Fact]
    public async Task Subscribe_UnknownNook_ReturnsNotFound()
    {
        if (OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection gui = await h.ConnectAsync("gui");
        JsonElement p = JsonSerializer.SerializeToElement(
            new SubscribeParams("nook-missing", 0), CoveJsonContext.Default.SubscribeParams);
        ControlResponse r = await RequestAsync(gui, "su", "cove://commands/nook.subscribe", p, ct);
        Assert.False(r.Ok);
        Assert.Equal("not_found", r.Error!.Code);
    }
}
