using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cove.Engine;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class TaskStatusRouteTests
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

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task Status_Create_List_Reorder_SetHidden_Delete_OverSocket()
    {
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var create = await SendAsync(ctl, "c", "cove://commands/task.status.create", P("""{"bayId":"ws1","id":"backlog","name":"Backlog","hexColor":"999999","position":10}"""), ct);
        Assert.True(create.Ok, create.Error?.Message);

        var list = await SendAsync(ctl, "l", "cove://commands/task.status.list", P("""{"bayId":"ws1"}"""), ct);
        Assert.True(list.Ok);
        var statusCount = list.Data!.Value.GetProperty("statuses").GetArrayLength();
        Assert.True(statusCount >= 6);

        var hidden = await SendAsync(ctl, "h", "cove://commands/task.status.set-hidden", P("""{"bayId":"ws1","id":"backlog","hidden":true}"""), ct);
        Assert.True(hidden.Ok);

        var del = await SendAsync(ctl, "d", "cove://commands/task.status.delete", P("""{"bayId":"ws1","id":"backlog","rehomeToStatusId":"todo"}"""), ct);
        Assert.True(del.Ok);

        var listAfter = await SendAsync(ctl, "l2", "cove://commands/task.status.list", P("""{"bayId":"ws1"}"""), ct);
        var ids = listAfter.Data!.Value.GetProperty("statuses").EnumerateArray().Select(s => s.GetProperty("id").GetString()).ToList();
        Assert.DoesNotContain("backlog", ids);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task Status_Create_ConflictOnDuplicateName()
    {
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var first = await SendAsync(ctl, "1", "cove://commands/task.status.create", P("""{"bayId":"ws1","id":"todo2","name":"Todo","hexColor":"808080","position":10}"""), ct);
        var second = await SendAsync(ctl, "2", "cove://commands/task.status.create", P("""{"bayId":"ws1","id":"todo3","name":"Todo","hexColor":"808080","position":11}"""), ct);
        Assert.False(second.Ok);
        Assert.Equal("conflict", second.Error!.Code);
    }
}
