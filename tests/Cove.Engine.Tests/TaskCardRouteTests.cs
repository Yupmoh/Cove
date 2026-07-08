using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cove.Engine;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class TaskCardRouteTests
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
    public async Task Create_Get_List_Update_Delete_OverSocket()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var create = await SendAsync(ctl, "c", "cove://commands/task.create", P("""{"title":"test card","workspaceId":"ws1","source":"user:test","priority":"high","size":"m"}"""), ct);
        Assert.True(create.Ok, create.Error?.Message);
        var cardId = create.Data!.Value.GetProperty("id").GetString()!;
        Assert.Equal(1, create.Data!.Value.GetProperty("taskNumber").GetInt32());
        Assert.Equal("COVE-1", create.Data!.Value.GetProperty("humanId").GetString());
        Assert.Equal(2, create.Data!.Value.GetProperty("priority").GetInt32());

        var get = await SendAsync(ctl, "g", "cove://commands/task.get", P($"{{\"id\":\"{cardId}\"}}"), ct);
        Assert.True(get.Ok);
        Assert.Equal("test card", get.Data!.Value.GetProperty("title").GetString());

        var list = await SendAsync(ctl, "l", "cove://commands/task.list", P("""{"workspaceId":"ws1"}"""), ct);
        Assert.True(list.Ok);
        Assert.Equal(1, list.Data!.Value.GetProperty("cards").GetArrayLength());

        var update = await SendAsync(ctl, "u", "cove://commands/task.update", P($"{{\"id\":\"{cardId}\",\"title\":\"updated\",\"source\":\"user:test2\"}}"), ct);
        Assert.True(update.Ok);
        Assert.Equal("updated", update.Data!.Value.GetProperty("title").GetString());
        Assert.Equal("user:test2", update.Data!.Value.GetProperty("source").GetString());

        var del = await SendAsync(ctl, "d", "cove://commands/task.delete", P($"{{\"id\":\"{cardId}\"}}"), ct);
        Assert.True(del.Ok);

        var getAfterDelete = await SendAsync(ctl, "g2", "cove://commands/task.get", P($"{{\"id\":\"{cardId}\"}}"), ct);
        Assert.False(getAfterDelete.Ok);
        Assert.Equal("not_found", getAfterDelete.Error!.Code);
    }

    [Fact]
    public async Task Create_NumberingIsMonotonicAcrossCards()
    {
        if (System.OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var c1 = await SendAsync(ctl, "1", "cove://commands/task.create", P("""{"title":"first","workspaceId":"ws1","source":"user:test"}"""), ct);
        var c2 = await SendAsync(ctl, "2", "cove://commands/task.create", P("""{"title":"second","workspaceId":"ws1","source":"user:test"}"""), ct);

        Assert.Equal(1, c1.Data!.Value.GetProperty("taskNumber").GetInt32());
        Assert.Equal(2, c2.Data!.Value.GetProperty("taskNumber").GetInt32());
        Assert.Equal("COVE-1", c1.Data!.Value.GetProperty("humanId").GetString());
        Assert.Equal("COVE-2", c2.Data!.Value.GetProperty("humanId").GetString());
    }
}
