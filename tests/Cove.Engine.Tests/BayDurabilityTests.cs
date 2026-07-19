using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cove.Engine.Bays;
using Cove.Protocol;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class BayDurabilityTests
{
    [Fact]
    public async Task ActiveBaySwitch_SurvivesRestartFromCanonicalWorkspaceState()
    {
        await using var h = await DaemonTestHarness.StartAsync();

        string seedId;
        await using (FrameConnection cli = await h.ConnectAsync("cli"))
        {
            seedId = (await ListBays(cli)).Single().GetProperty("id").GetString()!;
            await CreateBay(cli, "Second", System.IO.Path.GetTempPath());
            await Request(
                cli,
                "cove://commands/bay.switch",
                JsonSerializer.SerializeToElement(
                    new BayIdParams(seedId),
                    BaysJsonContext.Default.BayIdParams));
        }

        await h.RestartAsync();

        await using FrameConnection cli2 = await h.ConnectAsync("cli");
        var bays = await ListBays(cli2);
        Assert.True(bays.Single(bay => bay.GetProperty("id").GetString() == seedId)
            .GetProperty("active")
            .GetBoolean());
    }

    [Fact]
    public async Task SetIcon_OnNonActiveBay_SurvivesRestart_WithoutOtherEvent()
    {
        await using var h = await DaemonTestHarness.StartAsync();

        string seedId;
        await using (FrameConnection cli = await h.ConnectAsync("cli"))
        {
            seedId = (await ListBays(cli)).Single().GetProperty("id").GetString()!;
            await CreateBay(cli, "Second", System.IO.Path.GetTempPath());
            await Request(cli, "cove://commands/bay.set-icon",
                JsonSerializer.SerializeToElement(new BayIconParams(seedId, "emoji", "\U0001F680"), BayExtraJsonContext.Default.BayIconParams));
        }

        await h.RestartAsync();

        await using FrameConnection cli2 = await h.ConnectAsync("cli");
        var seed = (await ListBays(cli2)).Single(b => b.GetProperty("id").GetString() == seedId);
        Assert.True(seed.TryGetProperty("iconKind", out var kind), "iconKind missing after restart");
        Assert.Equal("emoji", kind.GetString());
        Assert.True(seed.TryGetProperty("iconValue", out var value), "iconValue missing after restart");
        Assert.Equal("\U0001F680", value.GetString());
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task Resize_PersistsColsRows_AcrossGracefulRestart()
    {

        await using var h = await DaemonTestHarness.StartAsync();

        string nookId;
        await using (FrameConnection cli = await h.ConnectAsync("cli"))
        {
            var spawn = await Request(cli, "cove://commands/nook.spawn",
                JsonSerializer.SerializeToElement(new SpawnParams("/bin/sh", new[] { "-c", "sleep 120" }, System.IO.Path.GetTempPath(), null, 80, 24), CoveJsonContext.Default.SpawnParams));
            nookId = spawn.Data!.Value.GetProperty("nookId").GetString()!;

            await Request(cli, "cove://commands/layout.mutate",
                JsonSerializer.SerializeToElement(new LayoutMutateParams("createShore", Name: "shell", NewNookId: nookId, NookType: "terminal"), CoveJsonContext.Default.LayoutMutateParams));

            var resized = await Request(cli, "cove://commands/nook.resize",
                JsonSerializer.SerializeToElement(new ResizeParams(nookId, 200, 60), CoveJsonContext.Default.ResizeParams));
            Assert.True(resized.Ok);
        }

        await h.RestartAsync();

        await using FrameConnection cli2 = await h.ConnectAsync("cli");
        var list = await Request(cli2, "cove://commands/nook.list", null);
        var restored = list.Data!.Value.GetProperty("nooks").EnumerateArray()
            .Single(n => n.GetProperty("nookId").GetString() == nookId);
        Assert.Equal(200, restored.GetProperty("cols").GetInt32());
        Assert.Equal(60, restored.GetProperty("rows").GetInt32());
    }

    private static async Task<JsonElement.ArrayEnumerator> ListBaysRaw(FrameConnection conn)
    {
        var r = await Request(conn, "cove://commands/bay.list", null);
        return r.Data!.Value.GetProperty("bays").EnumerateArray();
    }

    private static async Task<System.Collections.Generic.List<JsonElement>> ListBays(FrameConnection conn)
        => (await ListBaysRaw(conn)).ToList();

    private static Task<ControlResponse> CreateBay(FrameConnection conn, string name, string dir)
        => Request(conn, "cove://commands/bay.create",
            JsonSerializer.SerializeToElement(new BayCreateParams(name, dir), BaysJsonContext.Default.BayCreateParams));

    private static async Task<ControlResponse> Request(FrameConnection conn, string uri, JsonElement? prm)
    {
        await conn.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest(Guid.NewGuid().ToString("N"), uri, prm)), CancellationToken.None);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Frame resp = (await conn.ReadFrameAsync(cts.Token))!.Value;
        return ControlCodec.DecodeResponse(resp.Payload);
    }
}
