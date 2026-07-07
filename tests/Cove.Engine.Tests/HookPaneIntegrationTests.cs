using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class HookPaneIntegrationTests
{
    private static async Task<string> SpawnAsync(FrameConnection ctl, string command, string[] args, CancellationToken ct)
    {
        JsonElement sp = JsonSerializer.SerializeToElement(
            new SpawnParams(command, args, null, null, 80, 24),
            CoveJsonContext.Default.SpawnParams);
        ControlResponse r = await RequestAsync(ctl, "spawn", "cove://commands/pane.spawn", sp, ct);
        Assert.True(r.Ok, r.Error?.Message);
        PaneInfo info = r.Data!.Value.Deserialize(CoveJsonContext.Default.PaneInfo)!;
        return info.PaneId;
    }

    private static async Task<ControlResponse> RequestAsync(FrameConnection ctl, string id, string uri, JsonElement? p, CancellationToken ct)
    {
        await ctl.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest(id, uri, p)), ct);
        Frame resp = (await ctl.ReadFrameAsync(ct))!.Value;
        return ControlCodec.DecodeResponse(resp.Payload);
    }

    private static async Task PostHookAsync(HttpClient http, int port, string adapter, string ev, string paneId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}/api/adapter/{adapter}/{ev}");
        req.Headers.Add("X-Cove-Pane-Id", paneId);
        req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task HookPost_SessionStart_PopulatesPaneState()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(20));
        var ct = cts.Token;
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string paneId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "sleep 30" }, ct);

        string portFile = System.IO.Path.Combine(h.DataDir, "hook-port");
        Assert.True(System.IO.File.Exists(portFile), "hook-port file missing");
        int port = int.Parse(await System.IO.File.ReadAllTextAsync(portFile, ct));

        using var http = new HttpClient();
        await PostHookAsync(http, port, "test-v2", "session-start", paneId, ct);

        ControlResponse stateResp = await RequestAsync(ctl, "st", "cove://hooks/pane-states", null, ct);
        Assert.True(stateResp.Ok, stateResp.Error?.Message);
        var panes = stateResp.Data!.Value.GetProperty("panes");
        Assert.True(panes.GetArrayLength() >= 1, "pane-states should include the hook-posted pane");
        bool found = false;
        foreach (var p in panes.EnumerateArray())
            if (p.GetProperty("paneId").GetString() == paneId) found = true;
        Assert.True(found, $"pane {paneId} not found in pane-states after hook POST");
    }

    [Fact]
    public async Task HookPost_StopTransitions_PaneToWaitingForInput()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(20));
        var ct = cts.Token;
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string paneId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "sleep 30" }, ct);

        string portFile = System.IO.Path.Combine(h.DataDir, "hook-port");
        int port = int.Parse(await System.IO.File.ReadAllTextAsync(portFile, ct));

        using var http = new HttpClient();
        await PostHookAsync(http, port, "test-v2", "session-start", paneId, ct);
        await PostHookAsync(http, port, "test-v2", "stop", paneId, ct);

        ControlResponse stateResp = await RequestAsync(ctl, "st", "cove://hooks/pane-states", null, ct);
        Assert.True(stateResp.Ok);
        var panes = stateResp.Data!.Value.GetProperty("panes");
        string status = "";
        foreach (var p in panes.EnumerateArray())
            if (p.GetProperty("paneId").GetString() == paneId)
                status = p.GetProperty("status").GetString()!;
        Assert.Equal("needs-input", status);
    }

    [Fact]
    public async Task HookPost_UnknownAdapter_StillAcceptsEvent()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(20));
        var ct = cts.Token;
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string paneId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "sleep 30" }, ct);

        string portFile = System.IO.Path.Combine(h.DataDir, "hook-port");
        int port = int.Parse(await System.IO.File.ReadAllTextAsync(portFile, ct));

        using var http = new HttpClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}/api/adapter/never-installed/session-start");
        req.Headers.Add("X-Cove-Pane-Id", paneId);
        req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var postResp = await http.SendAsync(req, ct);
        Assert.True(postResp.IsSuccessStatusCode);
    }
}
