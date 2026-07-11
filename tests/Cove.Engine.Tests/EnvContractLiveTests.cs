using System.Text;
using System.Text.Json;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class EnvContractLiveTests
{
    [Fact]
    public async Task SpawnedNook_EnvContains_AllCoveVars_OverSocket()
    {
        if (System.OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        JsonElement sp = JsonSerializer.SerializeToElement(
            new SpawnParams("/bin/sh", new[] { "-c", "env; sleep 5" }, null, null, 80, 24),
            CoveJsonContext.Default.SpawnParams);
        ControlResponse spawnResp = await RequestAsync(ctl, "spawn", "cove://commands/nook.spawn", sp, ct);
        Assert.True(spawnResp.Ok, spawnResp.Error?.Message);
        string nookId = spawnResp.Data!.Value.Deserialize(CoveJsonContext.Default.NookInfo)!.NookId;

        JsonElement rp = JsonSerializer.SerializeToElement(
            new NookReadParams(nookId, 0, 65536),
            CoveJsonContext.Default.NookReadParams);

        string output = "";
        var deadline = Task.Delay(System.TimeSpan.FromSeconds(60), ct);
        while (!deadline.IsCompleted)
        {
            ControlResponse readResp = await RequestAsync(ctl, "read", "cove://commands/nook.read", rp, ct);
            Assert.True(readResp.Ok, readResp.Error?.Message);
            var result = readResp.Data!.Value.Deserialize(CoveJsonContext.Default.NookReadResult)!;
            if (!string.IsNullOrEmpty(result.DataBase64))
            {
                output = Encoding.UTF8.GetString(System.Convert.FromBase64String(result.DataBase64));
                if (output.Contains("COVE=1"))
                    break;
            }
            await Task.Delay(100, ct);
        }

        Assert.Contains("COVE=1", output);
        Assert.Contains("COVE_CLI_PATH=", output);
        Assert.Contains("COVE_DATA_DIR=", output);
        Assert.Contains("COVE_NOOK_ID=" + nookId, output);
        Assert.Contains("COVE_BAY_ID=", output);
        Assert.Contains("COVE_TASK_ID=", output);
        Assert.Contains("COVE_TASK_RUN_ID=", output);
        Assert.Contains("COVE_HOOK_PORT=", output);
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
