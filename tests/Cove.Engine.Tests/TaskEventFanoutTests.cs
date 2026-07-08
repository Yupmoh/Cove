using System.Text.Json;
using Cove.Engine;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class TaskEventFanoutTests
{
    private static JsonElement P(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static async Task<ControlResponse> SendAsync(FrameConnection ctl, string id, string uri, JsonElement? p, CancellationToken ct)
    {
        await ctl.WriteFrameAsync(FrameType.Request, 0, ControlCodec.Encode(new ControlRequest(id, uri, p)), ct);
        while (true)
        {
            Frame f = (await ctl.ReadFrameAsync(ct))!.Value;
            if (f.Header.Type != FrameType.Response) continue;
            ControlResponse r = ControlCodec.DecodeResponse(f.Payload);
            if (r.Id == id) return r;
        }
    }

    private static async Task<ControlEvent?> ReadEventAsync(FrameConnection ctl, CancellationToken ct, int timeoutMs = 2000)
    {
        using var eventCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        eventCts.CancelAfter(timeoutMs);
        try
        {
            while (true)
            {
                Frame? f = await ctl.ReadFrameAsync(eventCts.Token);
                if (f is not { } frame) return null;
                if (frame.Header.Type == FrameType.Event)
                    return ControlCodec.DecodeEvent(frame.Payload);
            }
        }
        catch (OperationCanceledException) { return null; }
    }

    [Fact]
    public async Task TaskCreate_EmitsCardChangedEventToGuiClient()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection gui = await h.ConnectAsync("gui");
        await using FrameConnection cli = await h.ConnectAsync("cli");

        await SendAsync(cli, "c", "cove://commands/task.create", P("""{"title":"event card","workspaceId":"ws1","source":"user:test"}"""), ct);

        var evt = await ReadEventAsync(gui, ct);
        Assert.NotNull(evt);
        Assert.True(evt!.Channel == "task.card.changed" || evt.Channel == "state.changed", $"got {evt.Channel}");
    }

    [Fact]
    public async Task RunCreate_EmitsRunChangedEventToGuiClient()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection gui = await h.ConnectAsync("gui");
        await using FrameConnection cli = await h.ConnectAsync("cli");

        var createResp = await SendAsync(cli, "c", "cove://commands/task.create", P("""{"title":"run-event card","workspaceId":"ws1","source":"user:test"}"""), ct);
        var cardId = createResp.Data!.Value.GetProperty("id").GetString()!;

        await SendAsync(cli, "cl", "cove://commands/task.claim", P($"{{\"cardId\":\"{cardId}\"}}"), ct);

        var evt = await ReadEventAsync(gui, ct);
        Assert.NotNull(evt);
        Assert.True(evt!.Channel == "task.run.changed" || evt.Channel == "state.changed", $"got {evt.Channel}");
    }

    [Fact]
    public async Task StatusCreate_EmitsBoardInvalidatedEventToGuiClient()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection gui = await h.ConnectAsync("gui");
        await using FrameConnection cli = await h.ConnectAsync("cli");

        await SendAsync(cli, "s", "cove://commands/task.status.create", P("""{"workspaceId":"ws1","id":"blocked","name":"Blocked","hexColor":"#ff0000","position":6.0}"""), ct);

        var evt = await ReadEventAsync(gui, ct);
        Assert.NotNull(evt);
        Assert.True(evt!.Channel == "task.board.invalidated" || evt.Channel == "state.changed", $"got {evt.Channel}");
    }
}
