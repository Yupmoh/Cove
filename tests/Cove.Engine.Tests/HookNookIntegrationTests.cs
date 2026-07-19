using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class HookNookIntegrationTests
{
    private static async Task<string> SpawnAsync(FrameConnection ctl, string command, string[] args, CancellationToken ct)
    {
        JsonElement sp = JsonSerializer.SerializeToElement(
            new SpawnParams(command, args, null, null, 80, 24),
            CoveJsonContext.Default.SpawnParams);
        ControlResponse r = await RequestAsync(ctl, "spawn", "cove://commands/nook.spawn", sp, ct);
        Assert.True(r.Ok, r.Error?.Message);
        NookInfo info = r.Data!.Value.Deserialize(CoveJsonContext.Default.NookInfo)!;
        return info.NookId;
    }

    private static async Task<ControlResponse> RequestAsync(FrameConnection ctl, string id, string uri, JsonElement? p, CancellationToken ct)
    {
        await ctl.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest(id, uri, p)), ct);
        Frame resp = (await ctl.ReadFrameAsync(ct))!.Value;
        return ControlCodec.DecodeResponse(resp.Payload);
    }

    private static async Task PostHookAsync(HttpClient http, int port, string adapter, string ev, string nookId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}/api/adapter/{adapter}/{ev}");
        req.Headers.Add("X-Cove-Nook-Id", nookId);
        req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task HookPost_SessionStart_PopulatesNookState()
    {
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(20));
        var ct = cts.Token;
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string nookId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "sleep 30" }, ct);

        string portFile = System.IO.Path.Combine(h.DataDir, "hook-port");
        Assert.True(System.IO.File.Exists(portFile), "hook-port file missing");
        int port = int.Parse(await System.IO.File.ReadAllTextAsync(portFile, ct));

        using var http = new HttpClient();
        await PostHookAsync(http, port, "test-v2", "session-start", nookId, ct);

        ControlResponse stateResp = await RequestAsync(ctl, "st", "cove://hooks/nook-states", null, ct);
        Assert.True(stateResp.Ok, stateResp.Error?.Message);
        var nooks = stateResp.Data!.Value.GetProperty("nooks");
        Assert.True(nooks.GetArrayLength() >= 1, "nook-states should include the hook-posted nook");
        bool found = false;
        foreach (var p in nooks.EnumerateArray())
            if (p.GetProperty("nookId").GetString() == nookId) found = true;
        Assert.True(found, $"nook {nookId} not found in nook-states after hook POST");
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task HookPost_StopTransitions_NookToDone()
    {
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(20));
        var ct = cts.Token;
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string nookId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "sleep 30" }, ct);

        string portFile = System.IO.Path.Combine(h.DataDir, "hook-port");
        int port = int.Parse(await System.IO.File.ReadAllTextAsync(portFile, ct));

        using var http = new HttpClient();
        await PostHookAsync(http, port, "test-v2", "session-start", nookId, ct);
        await PostHookAsync(http, port, "test-v2", "stop", nookId, ct);

        ControlResponse stateResp = await RequestAsync(ctl, "st", "cove://hooks/nook-states", null, ct);
        Assert.True(stateResp.Ok);
        var nooks = stateResp.Data!.Value.GetProperty("nooks");
        string status = "";
        foreach (var p in nooks.EnumerateArray())
            if (p.GetProperty("nookId").GetString() == nookId)
                status = p.GetProperty("status").GetString()!;
        Assert.Equal("done", status);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task HookPost_NotificationTransitions_NookToNeedsInput()
    {
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(20));
        var ct = cts.Token;
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string nookId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "sleep 30" }, ct);

        string portFile = System.IO.Path.Combine(h.DataDir, "hook-port");
        int port = int.Parse(await System.IO.File.ReadAllTextAsync(portFile, ct));

        using var http = new HttpClient();
        await PostHookAsync(http, port, "test-v2", "session-start", nookId, ct);
        await PostHookAsync(http, port, "test-v2", "notification", nookId, ct);

        ControlResponse stateResp = await RequestAsync(ctl, "st", "cove://hooks/nook-states", null, ct);
        Assert.True(stateResp.Ok);
        var nooks = stateResp.Data!.Value.GetProperty("nooks");
        string status = "";
        foreach (var p in nooks.EnumerateArray())
            if (p.GetProperty("nookId").GetString() == nookId)
                status = p.GetProperty("status").GetString()!;
        Assert.Equal("needs-input", status);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task HookPost_UnknownAdapter_StillAcceptsEvent()
    {
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(20));
        var ct = cts.Token;
        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string nookId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", "sleep 30" }, ct);

        string portFile = System.IO.Path.Combine(h.DataDir, "hook-port");
        int port = int.Parse(await System.IO.File.ReadAllTextAsync(portFile, ct));

        using var http = new HttpClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}/api/adapter/never-installed/session-start");
        req.Headers.Add("X-Cove-Nook-Id", nookId);
        req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var postResp = await http.SendAsync(req, ct);
        Assert.True(postResp.IsSuccessStatusCode);
    }
}
