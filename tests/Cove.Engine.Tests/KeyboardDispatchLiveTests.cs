using System.Text;
using System.Text.Json;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class KeyboardDispatchLiveTests
{
    [Theory]
    [InlineData("\u0003", "SIGINT Ctrl-C")]
    [InlineData("\u0001", "line-start Ctrl-A")]
    [InlineData("\u0005", "line-end Ctrl-E")]
    [InlineData("\u0015", "kill-line Ctrl-U")]
    [InlineData("\n", "newline Shift-Enter")]
    public async Task SendText_Passthrough_ReachesPty(string input, string label)
    {
        if (System.OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        string marker = "KB_" + label.Replace(" ", "_").Replace("/", "_");
        string nookId = await SpawnAsync(ctl, "/bin/sh", new[] { "-c", $"printf '%s\\n' '{marker}'; sleep 30" }, ct);
        await Task.Delay(500, ct);

        JsonElement wp = JsonSerializer.SerializeToElement(
            new NookWriteParams(nookId, System.Convert.ToBase64String(Encoding.UTF8.GetBytes(input))),
            CoveJsonContext.Default.NookWriteParams);
        ControlResponse writeResp = await RequestAsync(ctl, "w", "cove://commands/nook.write", wp, ct);
        Assert.True(writeResp.Ok, writeResp.Error?.Message);

        JsonElement rp = JsonSerializer.SerializeToElement(new NookReadParams(nookId, 0, 65536), CoveJsonContext.Default.NookReadParams);
        var deadline = Task.Delay(System.TimeSpan.FromSeconds(30), ct);
        string output = "";
        while (!deadline.IsCompleted)
        {
            ControlResponse r = await RequestAsync(ctl, "rd", "cove://commands/nook.read", rp, ct);
            if (r.Ok)
            {
                var result = r.Data!.Value.Deserialize(CoveJsonContext.Default.NookReadResult)!;
                if (!string.IsNullOrEmpty(result.DataBase64))
                {
                    output = Encoding.UTF8.GetString(System.Convert.FromBase64String(result.DataBase64));
                    if (output.Contains(marker)) break;
                }
            }
            await Task.Delay(100, ct);
        }
        Assert.Contains(marker, output);
    }

    private static async Task<string> SpawnAsync(FrameConnection ctl, string command, string[] args, CancellationToken ct)
    {
        JsonElement sp = JsonSerializer.SerializeToElement(
            new SpawnParams(command, args, null, null, 80, 24),
            CoveJsonContext.Default.SpawnParams);
        ControlResponse r = await RequestAsync(ctl, "spawn", "cove://commands/nook.spawn", sp, ct);
        Assert.True(r.Ok, r.Error?.Message);
        return r.Data!.Value.Deserialize(CoveJsonContext.Default.NookInfo)!.NookId;
    }

    private static async Task<ControlResponse> RequestAsync(FrameConnection ctl, string id, string uri, JsonElement? p, CancellationToken ct)
    {
        await ctl.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest(id, uri, p)), ct);
        while (true)
        {
            Frame f = (await ctl.ReadFrameAsync(ct))!.Value;
            if (f.Header.Type != FrameType.Response)
                continue;
            ControlResponse r = ControlCodec.DecodeResponse(f.Payload);
            if (r.Id == id)
                return r;
        }
    }
}
