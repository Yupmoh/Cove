using System.Text;
using System.Text.Json;
using Cove.Engine.Daemon;
using Cove.Engine.Restart;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Microsoft.Extensions.Logging;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class RestorationTests
{
    private sealed class NoOpLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoOpDisposable.Instance;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        private sealed class NoOpDisposable : IDisposable { public static readonly NoOpDisposable Instance = new(); public void Dispose() { } }
    }

    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-restore-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void CleanShutdown_Marker_Lifecycle()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var svc = new RestorationService(dir, new NoOpLogger());

            Assert.False(svc.WasCleanShutdown());

            svc.MarkLaunching();
            Assert.False(svc.WasCleanShutdown());

            svc.MarkCleanShutdown();
            Assert.True(svc.WasCleanShutdown());

            svc.MarkLaunching();
            Assert.False(svc.WasCleanShutdown());
        }
        finally
        {
            Cove.Testing.TestDirectory.Delete(dir);
        }
    }

    [Fact]
    public void LoadState_ReturnsDefault_WhenNoStateFile()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var svc = new RestorationService(dir, new NoOpLogger());
            var state = svc.LoadState();
            Assert.Equal(1, state.SchemaVersion);
            Assert.Empty(state.OpenBays);
            Assert.Null(state.FocusedBay);
            Assert.False(state.CleanShutdown);
        }
        finally
        {
            Cove.Testing.TestDirectory.Delete(dir);
        }
    }

    [Fact]
    public void SaveState_PersistsAndReloads()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var svc = new RestorationService(dir, new NoOpLogger());
            svc.SaveState(new Cove.Persistence.CoveState
            {
                OpenBays = ["ws-1", "ws-2"],
                FocusedBay = "ws-2",
                CleanShutdown = true,
                ShutdownAtUtc = DateTimeOffset.UtcNow,
            });

            var loaded = svc.LoadState();
            Assert.Equal(2, loaded.OpenBays.Count);
            Assert.Equal("ws-2", loaded.FocusedBay);
            Assert.True(loaded.CleanShutdown);
        }
        finally
        {
            Cove.Testing.TestDirectory.Delete(dir);
        }
    }

    [Fact]
    public void EmitProgress_InvokesCallback()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        var events = new List<RestoreProgressEvent>();
        try
        {
            var svc = new RestorationService(dir, new NoOpLogger(), emitProgress: e => events.Add(e));
            svc.EmitProgress("ws-1", "load", RestorePhase.Started, "clean");
            svc.EmitProgress("ws-1", "done", RestorePhase.Completed);

            Assert.Equal(2, events.Count);
            Assert.Equal(RestorePhase.Started, events[0].Phase);
            Assert.Equal("clean", events[0].Detail);
            Assert.Equal(RestorePhase.Completed, events[1].Phase);
        }
        finally
        {
            Cove.Testing.TestDirectory.Delete(dir);
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task DaemonRestart_RestoresNooksAndScrollback_IdenticalState()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        const string marker = "COVE_RESTART_PROOF";
        JsonElement sp = JsonSerializer.SerializeToElement(
            new SpawnParams("/bin/sh", new[] { "-c", "echo " + marker + "; sleep 30" }, null, null, 80, 24),
            CoveJsonContext.Default.SpawnParams);
        await ctl.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("1", "cove://commands/nook.spawn", sp)), ct);
        ControlResponse spawnResp = await ReadResponseAsync(ctl, "1", ct);
        Assert.True(spawnResp.Ok, spawnResp.Error?.Message);
        string nookId = spawnResp.Data!.Value.Deserialize(CoveJsonContext.Default.NookInfo)!.NookId;

        JsonElement mp = JsonSerializer.SerializeToElement(
            new LayoutMutateParams("createShore", NewNookId: nookId, Name: "main"),
            Cove.Protocol.CoveJsonContext.Default.LayoutMutateParams);
        await ctl.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("2", "cove://commands/layout.mutate", mp)), ct);
        ControlResponse mutateResp = await ReadResponseAsync(ctl, "2", ct);
        Assert.True(mutateResp.Ok, mutateResp.Error?.Message);

        await AwaitNookOutputAsync(ctl, nookId, marker, ct);

        await h.RestartAsync();
        await using FrameConnection ctl2 = await h.ConnectAsync("cli");

        JsonElement lp = JsonSerializer.SerializeToElement(
            new SubscribeParams(nookId, 0), CoveJsonContext.Default.SubscribeParams);
        await ctl2.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("3", "cove://commands/nook.subscribe", lp)), ct);
        ControlResponse subResp = await ReadResponseAsync(ctl2, "3", ct);
        Assert.True(subResp.Ok, subResp.Error?.Message);

        var buf = new byte[256];
        int total = 0;
        var deadline = Task.Delay(TimeSpan.FromSeconds(5), ct);
        while (total < marker.Length)
        {
            if (deadline.IsCompleted)
                break;
            Frame f = (await ctl2.ReadFrameAsync(ct))!.Value;
            if (f.Header.Type == FrameType.Response)
                continue;
            int n = Math.Min(buf.Length - total, f.Payload.Length);
            Array.Copy(f.Payload, 0, buf, total, n);
            total += n;
        }
        string restored = Encoding.ASCII.GetString(buf, 0, total);
        Assert.Contains(marker, restored);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task DaemonRestart_RestoresCheckpointAndRawTail()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        JsonElement spawnParams = JsonSerializer.SerializeToElement(
            new SpawnParams("/bin/cat", Array.Empty<string>(), null, null, 80, 24),
            CoveJsonContext.Default.SpawnParams);
        await ctl.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("1", "cove://commands/nook.spawn", spawnParams)), ct);
        ControlResponse spawnResponse = await ReadResponseAsync(ctl, "1", ct);
        Assert.True(spawnResponse.Ok, spawnResponse.Error?.Message);
        string nookId = spawnResponse.Data!.Value.Deserialize(CoveJsonContext.Default.NookInfo)!.NookId;

        JsonElement layoutParams = JsonSerializer.SerializeToElement(
            new LayoutMutateParams("createShore", NewNookId: nookId, Name: "main"),
            Cove.Protocol.CoveJsonContext.Default.LayoutMutateParams);
        await ctl.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("2", "cove://commands/layout.mutate", layoutParams)), ct);
        Assert.True((await ReadResponseAsync(ctl, "2", ct)).Ok);

        const string beforeCheckpoint = "BEFORE_CHECKPOINT\n";
        JsonElement beforeWrite = JsonSerializer.SerializeToElement(
            new NookWriteParams(nookId, Convert.ToBase64String(Encoding.ASCII.GetBytes(beforeCheckpoint))),
            CoveJsonContext.Default.NookWriteParams);
        await ctl.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("3", "cove://commands/nook.write", beforeWrite)), ct);
        Assert.True((await ReadResponseAsync(ctl, "3", ct)).Ok);
        var readResult = await AwaitNookOutputAsync(ctl, nookId, "BEFORE_CHECKPOINT", ct);
        Assert.Contains("BEFORE_CHECKPOINT", Encoding.ASCII.GetString(Convert.FromBase64String(readResult.DataBase64)));

        byte[] serializedState = Encoding.ASCII.GetBytes("SERIALIZED_BEFORE_CHECKPOINT");
        JsonElement checkpointParams = JsonSerializer.SerializeToElement(
            new NookCheckpointParams(nookId, Convert.ToBase64String(serializedState), readResult.Head, 80, 24, 10000),
            CoveJsonContext.Default.NookCheckpointParams);
        await ctl.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("5", "cove://commands/nook.checkpoint", checkpointParams)), ct);
        Assert.True((await ReadResponseAsync(ctl, "5", ct)).Ok);

        const string rawTail = "AFTER_CHECKPOINT\n";
        JsonElement tailWrite = JsonSerializer.SerializeToElement(
            new NookWriteParams(nookId, Convert.ToBase64String(Encoding.ASCII.GetBytes(rawTail))),
            CoveJsonContext.Default.NookWriteParams);
        await ctl.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("6", "cove://commands/nook.write", tailWrite)), ct);
        Assert.True((await ReadResponseAsync(ctl, "6", ct)).Ok);
        await AwaitNookOutputAsync(ctl, nookId, "AFTER_CHECKPOINT", ct);

        await h.RestartAsync();
        await using FrameConnection restoredConnection = await h.ConnectAsync("cli");
        JsonElement subscribeParams = JsonSerializer.SerializeToElement(
            new SubscribeParams(nookId, 0), CoveJsonContext.Default.SubscribeParams);
        await restoredConnection.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("7", "cove://commands/nook.subscribe", subscribeParams)), ct);
        ControlResponse subscribeResponse = await ReadResponseAsync(restoredConnection, "7", ct);
        Assert.True(subscribeResponse.Ok, subscribeResponse.Error?.Message);
        var subscription = subscribeResponse.Data!.Value.Deserialize(CoveJsonContext.Default.SubscribeResult)!;
        Assert.Equal(Convert.ToBase64String(serializedState), subscription.TerminalCheckpointBase64);
        Assert.Equal(80, subscription.CheckpointCols);
        Assert.Equal(24, subscription.CheckpointRows);

        var restoredTail = new MemoryStream();
        var deadline = Task.Delay(TimeSpan.FromSeconds(5), ct);
        while (!deadline.IsCompleted && restoredTail.Length < rawTail.Length)
        {
            Frame frame = (await restoredConnection.ReadFrameAsync(ct))!.Value;
            if (frame.Header.Type != FrameType.StreamData)
                continue;
            restoredTail.Write(frame.Payload, 8, frame.Payload.Length - 8);
        }
        Assert.Contains("AFTER_CHECKPOINT", Encoding.ASCII.GetString(restoredTail.ToArray()));
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task DaemonRestart_CapturesHookSessionId_PersistsOntoNookRecord_AndRespawnsSameNook()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        const string marker = "COVE_AGENT_RESTART_PROOF";
        JsonElement sp = JsonSerializer.SerializeToElement(
            new SpawnParams("/bin/sh", new[] { "-c", "echo " + marker + "; sleep 30" }, null, null, 80, 24, null, "test-v2", "claude"),
            CoveJsonContext.Default.SpawnParams);
        await ctl.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("1", "cove://commands/nook.spawn", sp)), ct);
        ControlResponse spawnResp = await ReadResponseAsync(ctl, "1", ct);
        Assert.True(spawnResp.Ok, spawnResp.Error?.Message);
        string nookId = spawnResp.Data!.Value.Deserialize(CoveJsonContext.Default.NookInfo)!.NookId;

        JsonElement mp = JsonSerializer.SerializeToElement(
            new LayoutMutateParams("createShore", NewNookId: nookId, Name: "main"),
            Cove.Protocol.CoveJsonContext.Default.LayoutMutateParams);
        await ctl.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("2", "cove://commands/layout.mutate", mp)), ct);
        Assert.True((await ReadResponseAsync(ctl, "2", ct)).Ok);

        const string adapterSessionId = "sess-live-1234";
        var portPath = Path.Combine(h.DataDir, "hook-port");
        var portDeadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!File.Exists(portPath) && DateTimeOffset.UtcNow < portDeadline)
            await Task.Delay(50, ct);
        var port = int.Parse(await File.ReadAllTextAsync(portPath, ct));
        using (var http = new System.Net.Http.HttpClient())
        {
            using var msg = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post,
                $"http://127.0.0.1:{port}/api/adapter/test-v2/session-start");
            msg.Content = new System.Net.Http.StringContent(
                "{\"session_id\":\"" + adapterSessionId + "\"}", Encoding.UTF8, "application/json");
            msg.Headers.Add("X-Cove-Nook-Id", nookId);
            var resp = await http.SendAsync(msg, ct);
            Assert.True(resp.IsSuccessStatusCode);
        }

        var sessionFile = Path.Combine(h.DataDir, "bays");
        string? found = null;
        var persistDeadline = DateTimeOffset.UtcNow.AddSeconds(8);
        while (DateTimeOffset.UtcNow < persistDeadline)
        {
            if (Directory.Exists(sessionFile))
            {
                var candidate = Directory.EnumerateFiles(sessionFile, "session.json", SearchOption.AllDirectories)
                    .FirstOrDefault(f => f.Contains(nookId, StringComparison.Ordinal));
                if (candidate is not null && File.ReadAllText(candidate).Contains(adapterSessionId, StringComparison.Ordinal))
                {
                    found = candidate;
                    break;
                }
            }
            await Task.Delay(150, ct);
        }
        Assert.NotNull(found);
        using (var doc = JsonDocument.Parse(await File.ReadAllTextAsync(found!, ct)))
        {
            Assert.Equal(adapterSessionId, doc.RootElement.GetProperty("sessionId").GetString());
            Assert.Equal("test-v2", doc.RootElement.GetProperty("adapter").GetString());
        }

        await h.RestartAsync();
        await using FrameConnection ctl2 = await h.ConnectAsync("cli");

        JsonElement lp = JsonSerializer.SerializeToElement(
            new SubscribeParams(nookId, 0), CoveJsonContext.Default.SubscribeParams);
        await ctl2.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("3", "cove://commands/nook.subscribe", lp)), ct);
        ControlResponse subResp = await ReadResponseAsync(ctl2, "3", ct);
        Assert.True(subResp.Ok, subResp.Error?.Message);

        var buf = new byte[256];
        int total = 0;
        var deadline = Task.Delay(TimeSpan.FromSeconds(5), ct);
        while (total < marker.Length)
        {
            if (deadline.IsCompleted)
                break;
            Frame f = (await ctl2.ReadFrameAsync(ct))!.Value;
            if (f.Header.Type == FrameType.Response)
                continue;
            int n = Math.Min(buf.Length - total, f.Payload.Length);
            Array.Copy(f.Payload, 0, buf, total, n);
            total += n;
        }
        Assert.Contains(marker, Encoding.ASCII.GetString(buf, 0, total));
    }

    private static async Task<NookReadResult> AwaitNookOutputAsync(
        FrameConnection connection,
        string nookId,
        string marker,
        CancellationToken cancellationToken)
    {
        NookReadResult? observed = null;
        await AsyncTest.EventuallyAsync(async () =>
        {
            var parameters = JsonSerializer.SerializeToElement(
                new NookReadParams(nookId, 0, 65536),
                CoveJsonContext.Default.NookReadParams);
            var id = $"await-{marker}";
            await connection.WriteFrameAsync(FrameType.Request, 0,
                ControlCodec.Encode(new ControlRequest(id, "cove://commands/nook.read", parameters)), cancellationToken);
            var response = await ReadResponseAsync(connection, id, cancellationToken);
            if (!response.Ok)
                return false;
            observed = response.Data!.Value.Deserialize(CoveJsonContext.Default.NookReadResult);
            return observed is not null
                && !string.IsNullOrEmpty(observed.DataBase64)
                && Encoding.UTF8.GetString(Convert.FromBase64String(observed.DataBase64)).Contains(marker, StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(10), $"nook output did not contain {marker}", cancellationToken);
        return observed!;
    }

    private static async Task<ControlResponse> ReadResponseAsync(FrameConnection conn, string id, CancellationToken ct)
    {
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
}
