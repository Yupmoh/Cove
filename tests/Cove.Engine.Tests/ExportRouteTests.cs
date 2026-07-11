using System.Text.Json;
using Cove.Engine;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ExportRouteTests
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

    [Fact]
    public async Task Export_ProducesConsistentCopyWithManifest()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        await SendAsync(ctl, "c", "cove://commands/task.create", P("""{"title":"export card","bayId":"ws1","source":"user:test"}"""), ct);

        var exportPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-export-" + System.Guid.NewGuid().ToString("N") + ".db");
        var exportResp = await SendAsync(ctl, "e", "cove://commands/task-board.export", P($"{{\"exportPath\":\"{exportPath}\",\"bayCount\":1}}"), ct);
        Assert.True(exportResp.Ok, exportResp.Error?.Code);
        Assert.True(exportResp.Data!.Value.GetProperty("success").GetBoolean());
        Assert.Equal(1, exportResp.Data!.Value.GetProperty("schemaVersion").GetInt32());
        Assert.True(System.IO.File.Exists(exportPath));

        try { System.IO.File.Delete(exportPath); } catch { }
    }

    [Fact]
    public async Task Diff_GeneratesRowDifferences()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var exportPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-diff-" + System.Guid.NewGuid().ToString("N") + ".db");
        await SendAsync(ctl, "c", "cove://commands/task.create", P("""{"title":"diff card 1","bayId":"ws1","source":"user:test"}"""), ct);
        await SendAsync(ctl, "e", "cove://commands/task-board.export", P($"{{\"exportPath\":\"{exportPath}\",\"bayCount\":1}}"), ct);

        await SendAsync(ctl, "c2", "cove://commands/task.create", P("""{"title":"diff card 2","bayId":"ws1","source":"user:test"}"""), ct);

        var diffResp = await SendAsync(ctl, "d", "cove://commands/task-board.diff", P($"{{\"importPath\":\"{exportPath}\"}}"), ct);
        Assert.True(diffResp.Ok, diffResp.Error?.Code);
        Assert.True(diffResp.Data!.Value.GetProperty("success").GetBoolean());
        Assert.True(diffResp.Data!.Value.GetProperty("diffs").GetArrayLength() > 0);

        try { System.IO.File.Delete(exportPath); } catch { }
    }
}
