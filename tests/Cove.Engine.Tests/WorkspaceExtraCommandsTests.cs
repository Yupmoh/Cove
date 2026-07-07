using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Engine;
using Cove.Engine.Workspaces;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class WorkspaceExtraCommandsTests
{
    private static Task<ControlResponse?> Route(WorkspaceManager m, string uri, JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, null, m);

    private static JsonElement El<T>(T v, JsonTypeInfo<T> ti) => JsonSerializer.SerializeToElement(v, ti);

    [Fact]
    public async Task Hide_Icon_Accent_Apply()
    {
        int n = 0;
        await using var m = new WorkspaceManager(newId: () => $"id-{++n}");
        var ws = await m.CreateWorkspaceAsync("a", "/a");

        await Route(m, "cove://commands/workspace.hide",
            El(new WorkspaceHideParams(ws.Id, true), WorkspaceExtraJsonContext.Default.WorkspaceHideParams));
        await Route(m, "cove://commands/workspace.set-accent",
            El(new WorkspaceAccentParams(ws.Id, "#ff8800"), WorkspaceExtraJsonContext.Default.WorkspaceAccentParams));
        await Route(m, "cove://commands/workspace.set-icon",
            El(new WorkspaceIconParams(ws.Id, "emoji", "rocket"), WorkspaceExtraJsonContext.Default.WorkspaceIconParams));

        var st = m.Get(ws.Id)!.State;
        Assert.True(st.Hidden);
        Assert.Equal("#ff8800", st.AccentColor);
        Assert.Equal("emoji", st.Icon!.Kind);
        Assert.Equal("rocket", st.Icon.Value);
    }

    [Fact]
    public async Task Reorder_ReordersOpenWorkspaces()
    {
        int n = 0;
        await using var m = new WorkspaceManager(newId: () => $"id-{++n}");
        var a = await m.CreateWorkspaceAsync("a", "/a");
        var b = await m.CreateWorkspaceAsync("b", "/b");
        var c = await m.CreateWorkspaceAsync("c", "/c");

        await Route(m, "cove://commands/workspace.reorder",
            El(new WorkspaceReorderParams(new[] { c.Id, a.Id, b.Id }), WorkspaceExtraJsonContext.Default.WorkspaceReorderParams));
        Assert.Equal(new[] { c.Id, a.Id, b.Id }, m.Registry.OpenWorkspaces.ToArray());
    }

    [Fact]
    public async Task MoveRoom_MovesBetweenWorkspaces()
    {
        int n = 0;
        await using var m = new WorkspaceManager(newId: () => $"id-{++n}");
        var a = await m.CreateWorkspaceAsync("a", "/a");
        var b = await m.CreateWorkspaceAsync("b", "/b");

        var created = await Route(m, "cove://commands/room.create",
            El(new RoomCreateParams(a.Id, null, "extra"), RoomWingJsonContext.Default.RoomCreateParams));
        var roomId = created!.Data!.Value.GetProperty("roomId").GetString()!;

        var moved = await Route(m, "cove://commands/workspace.move-room",
            El(new WorkspaceMoveRoomParams(a.Id, roomId, b.Id), WorkspaceExtraJsonContext.Default.WorkspaceMoveRoomParams));
        Assert.True(moved!.Ok);
        Assert.DoesNotContain(m.Get(a.Id)!.State.Rooms, r => r.Id == roomId);
        Assert.Contains(m.Get(b.Id)!.State.Rooms, r => r.Id == roomId);
    }
}
