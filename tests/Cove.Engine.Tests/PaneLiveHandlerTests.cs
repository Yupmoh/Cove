using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class PaneLiveHandlerTests
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
        ControlResponse r = await RequestAsync(conn, "sp", "cove://commands/pane.spawn", p, ct);
        Assert.True(r.Ok, r.Error?.Message);
        PaneInfo info = r.Data!.Value.Deserialize(CoveJsonContext.Default.PaneInfo)!;
        return info.PaneId;
    }

    private static async Task<PaneInfo[]> ListAsync(FrameConnection conn, CancellationToken ct)
    {
        ControlResponse r = await RequestAsync(conn, "pl", "cove://commands/pane.list", null, ct);
        Assert.True(r.Ok);
        return r.Data!.Value.Deserialize(CoveJsonContext.Default.PaneListResult)!.Panes;
    }

    private static async Task<SubscribeResult> SubscribeAsync(
        FrameConnection conn, string paneId, ulong since, CancellationToken ct)
    {
        JsonElement p = JsonSerializer.SerializeToElement(
            new SubscribeParams(paneId, since), CoveJsonContext.Default.SubscribeParams);
        ControlResponse r = await RequestAsync(conn, "su", "cove://commands/pane.subscribe", p, ct);
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

    private static JsonElement WriteParamsJson(string paneId, string dataBase64)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("paneId", paneId);
            w.WriteString("dataBase64", dataBase64);
            w.WriteEndObject();
        }
        using var doc = JsonDocument.Parse(ms.ToArray());
        return doc.RootElement.Clone();
    }

    private static JsonElement PaneRefJson(string paneId)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("paneId", paneId);
            w.WriteEndObject();
        }
        using var doc = JsonDocument.Parse(ms.ToArray());
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task RunningPane_SurvivesClientClose_AndReplaysRingIdenticallyOnReattach()
    {
        if (OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        const string marker = "COVE_REATTACH_PROOF";
        string paneId = await SpawnAsync(
            ctl, "/bin/sh", new[] { "-c", "echo " + marker + "; sleep 30" }, 80, 24, ct);

        byte[] first;
        {
            await using FrameConnection a = await h.ConnectAsync("gui");
            SubscribeResult sub = await SubscribeAsync(a, paneId, 0, ct);
            first = await ReadRawAsync(a, sub.StreamId, marker.Length, ct);
        }

        PaneInfo[] afterClose = await ListAsync(ctl, ct);
        Assert.Contains(afterClose, p => p.PaneId == paneId && p.Alive);

        byte[] second;
        {
            await using FrameConnection b = await h.ConnectAsync("gui");
            SubscribeResult sub = await SubscribeAsync(b, paneId, 0, ct);
            second = await ReadRawAsync(b, sub.StreamId, marker.Length, ct);
        }

        Assert.Equal(marker, Encoding.ASCII.GetString(first));
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task PaneSpawn_ListsAlive_ThenKill_Removes()
    {
        if (OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string paneId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "sleep 30" }, 100, 40, ct);

        PaneInfo[] listed = await ListAsync(ctl, ct);
        PaneInfo pane = Assert.Single(listed, p => p.PaneId == paneId);
        Assert.True(pane.Alive);
        Assert.Equal(100, pane.Cols);
        Assert.Equal(40, pane.Rows);

        ControlResponse kill = await RequestAsync(ctl, "kl", "cove://commands/pane.kill", PaneRefJson(paneId), ct);
        Assert.True(kill.Ok, kill.Error?.Message);

        PaneInfo[] after = await ListAsync(ctl, ct);
        Assert.DoesNotContain(after, p => p.PaneId == paneId && p.Alive);

        ControlResponse missing = await RequestAsync(ctl, "kl2", "cove://commands/pane.kill", PaneRefJson("pane-does-not-exist"), ct);
        Assert.False(missing.Ok);
        Assert.Equal("not_found", missing.Error!.Code);
    }

    [Fact]
    public async Task PaneWrite_ReachesChild_AndOutputStreams()
    {
        if (OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string paneId = await SpawnAsync(ctl, "/bin/cat", Array.Empty<string>(), 80, 24, ct);
        await using FrameConnection sink = await h.ConnectAsync("gui");
        SubscribeResult sub = await SubscribeAsync(sink, paneId, 0, ct);

        const string ping = "COVE_PING\n";
        string b64 = Convert.ToBase64String(Encoding.ASCII.GetBytes(ping));
        ControlResponse w = await RequestAsync(ctl, "wr", "cove://commands/pane.write", WriteParamsJson(paneId, b64), ct);
        Assert.True(w.Ok, w.Error?.Message);

        byte[] echoed = await ReadRawAsync(sink, sub.StreamId, "COVE_PING".Length, ct);
        Assert.Contains("COVE_PING", Encoding.ASCII.GetString(echoed) + "COVE_PING");
        Assert.Equal("COVE_PING", Encoding.ASCII.GetString(echoed));
    }

    [Fact]
    public async Task PaneResize_LivePane_Ok_MissingPane_NotFound()
    {
        if (OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string paneId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "sleep 30" }, 80, 24, ct);
        JsonElement rp = JsonSerializer.SerializeToElement(
            new ResizeParams(paneId, 120, 50), CoveJsonContext.Default.ResizeParams);
        ControlResponse ok = await RequestAsync(ctl, "rs", "cove://commands/pane.resize", rp, ct);
        Assert.True(ok.Ok, ok.Error?.Message);

        JsonElement rpMissing = JsonSerializer.SerializeToElement(
            new ResizeParams("pane-nope", 120, 50), CoveJsonContext.Default.ResizeParams);
        ControlResponse missing = await RequestAsync(ctl, "rs2", "cove://commands/pane.resize", rpMissing, ct);
        Assert.False(missing.Ok);
        Assert.Equal("not_found", missing.Error!.Code);
    }

    [Fact]
    public async Task Subscribe_UnknownPane_ReturnsNotFound()
    {
        if (OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection gui = await h.ConnectAsync("gui");
        JsonElement p = JsonSerializer.SerializeToElement(
            new SubscribeParams("pane-missing", 0), CoveJsonContext.Default.SubscribeParams);
        ControlResponse r = await RequestAsync(gui, "su", "cove://commands/pane.subscribe", p, ct);
        Assert.False(r.Ok);
        Assert.Equal("not_found", r.Error!.Code);
    }
}
