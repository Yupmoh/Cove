using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Engine;
using Cove.Engine.Workspaces;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class RoomWingCommandsTests
{
    private static Task<ControlResponse?> Route(WorkspaceManager m, string uri, JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, null, m);

    private static JsonElement El<T>(T v, JsonTypeInfo<T> ti) => JsonSerializer.SerializeToElement(v, ti);

    [Fact]
    public async Task Room_Create_List_Rename_MoveWing_Close()
    {
        int n = 0;
        await using var mgr = new WorkspaceManager(newId: () => $"id-{++n}");
        var ws = await mgr.CreateWorkspaceAsync("proj", "/tmp/proj");
        var wsId = ws.Id;

        var created = await Route(mgr, "cove://commands/room.create",
            El(new RoomCreateParams(wsId, null, "build"), RoomWingJsonContext.Default.RoomCreateParams));
        Assert.True(created!.Ok);
        var roomId = created.Data!.Value.GetProperty("roomId").GetString()!;

        var listed = await Route(mgr, "cove://commands/room.list",
            El(new WorkspaceRef(wsId), RoomWingJsonContext.Default.WorkspaceRef));
        Assert.Equal(2, listed!.Data!.Value.GetProperty("rooms").GetArrayLength());

        await Route(mgr, "cove://commands/room.rename",
            El(new RoomRenameParams(wsId, roomId, "renamed"), RoomWingJsonContext.Default.RoomRenameParams));

        var wing = await Route(mgr, "cove://commands/wing.create",
            El(new WingCreateParams(wsId, "side"), RoomWingJsonContext.Default.WingCreateParams));
        var wingId = wing!.Data!.Value.GetProperty("wingId").GetString()!;
        await Route(mgr, "cove://commands/room.move-to-wing",
            El(new RoomMoveParams(wsId, roomId, wingId), RoomWingJsonContext.Default.RoomMoveParams));

        var room = mgr.Get(wsId)!.State.Rooms.First(r => r.Id == roomId);
        Assert.Equal("renamed", room.Name);
        Assert.Equal(wingId, room.WingId);

        var closed = await Route(mgr, "cove://commands/room.close",
            El(new RoomTargetParams(wsId, roomId), RoomWingJsonContext.Default.RoomTargetParams));
        Assert.True(closed!.Ok);
        Assert.Single(mgr.Get(wsId)!.State.Rooms);
    }

    [Fact]
    public async Task Wing_Create_List_Remove_RehomesRooms()
    {
        int n = 0;
        await using var mgr = new WorkspaceManager(newId: () => $"id-{++n}");
        var ws = await mgr.CreateWorkspaceAsync("proj", "/tmp/proj");

        var wing = await Route(mgr, "cove://commands/wing.create",
            El(new WingCreateParams(ws.Id, "side"), RoomWingJsonContext.Default.WingCreateParams));
        var wingId = wing!.Data!.Value.GetProperty("wingId").GetString()!;

        var listed = await Route(mgr, "cove://commands/wing.list",
            El(new WorkspaceRef(ws.Id), RoomWingJsonContext.Default.WorkspaceRef));
        Assert.Equal(2, listed!.Data!.Value.GetProperty("wings").GetArrayLength());

        var removed = await Route(mgr, "cove://commands/wing.remove",
            El(new WingTargetParams(ws.Id, wingId), RoomWingJsonContext.Default.WingTargetParams));
        Assert.True(removed!.Ok);
        Assert.DoesNotContain(mgr.Get(ws.Id)!.State.Wings, w => w.Id == wingId);
    }
}
