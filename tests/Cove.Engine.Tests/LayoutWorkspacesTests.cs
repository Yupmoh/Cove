using System.Linq;
using Cove.Engine.Layout;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class LayoutWorkspacesTests
{
    private static PaneLeaf Leaf(string id, PaneType type = PaneType.Terminal) => new()
    {
        PaneId = id,
        Subtabs = new[] { new Subtab(id, type) },
    };

    [Fact]
    public void SwitchWorkspace_SwapsRenderedRoomSet()
    {
        var svc = new LayoutService();
        svc.SetActiveWorkspace("wsA");
        var a = svc.CreateRoom("A-room", Leaf("a"));
        svc.SetActiveWorkspace("wsB");
        var b = svc.CreateRoom("B-room", Leaf("b"));

        var snapB = svc.ToSnapshot("wsB", "B", "/b");
        Assert.Single(snapB.Rooms);
        Assert.Equal(b, snapB.Rooms[0].Id);

        svc.SetActiveWorkspace("wsA");
        var snapA = svc.ToSnapshot("wsA", "A", "/a");
        Assert.Single(snapA.Rooms);
        Assert.Equal(a, snapA.Rooms[0].Id);
    }

    [Fact]
    public void ActiveWorkspaceId_TracksSwitch()
    {
        var svc = new LayoutService();
        Assert.Equal(LayoutService.DefaultWorkspaceId, svc.ActiveWorkspaceId);
        svc.SetActiveWorkspace("wsX");
        Assert.Equal("wsX", svc.ActiveWorkspaceId);
    }

    [Fact]
    public void RemoveWorkspace_ReturnsPaneIds_AndDropsRooms()
    {
        var svc = new LayoutService();
        svc.SetActiveWorkspace("wsA");
        var room = svc.CreateRoom("A", Leaf("a"));
        svc.SplitPane(room, "a", SplitOrientation.Row, Leaf("b"));

        var paneIds = svc.RemoveWorkspace("wsA");
        Assert.Contains("a", paneIds);
        Assert.Contains("b", paneIds);
        Assert.Empty(svc.ToSnapshot("wsA", "A", "/a").Rooms);
    }

    [Fact]
    public void EmptyRoom_IsLegal_AndSnapshots()
    {
        var svc = new LayoutService();
        var room = svc.CreateRoom("Terminal 1", Leaf("empty-1", PaneType.Empty));
        Assert.True(LayoutService.IsEmptyRoom(svc.GetRoot(room)!));
    }

    [Fact]
    public void ReplacePane_PutsPaneIntoEmptyRoom()
    {
        var svc = new LayoutService();
        var room = svc.CreateRoom("Terminal 1", Leaf("empty-1", PaneType.Empty));
        svc.ReplacePane(room, "empty-1", Leaf("real-1"));
        Assert.False(LayoutService.IsEmptyRoom(svc.GetRoot(room)!));
        Assert.Equal("real-1", svc.GetActive(room));
        var leaves = MosaicOps.Leaves(svc.GetRoot(room)!);
        Assert.Single(leaves);
        Assert.Equal("real-1", leaves[0].PaneId);
    }

    [Fact]
    public void ClosingLastPane_ConvertsRoomToEmpty_NotRemoved()
    {
        var svc = new LayoutService();
        var room = svc.CreateRoom("Terminal 1", Leaf("a"));
        svc.ClosePane(room, "a");
        var root = svc.GetRoot(room);
        Assert.NotNull(root);
        Assert.True(LayoutService.IsEmptyRoom(root!));
    }

    [Fact]
    public void CloseRoom_RemovesRoomEntirely()
    {
        var svc = new LayoutService();
        var room = svc.CreateRoom("Terminal 1", Leaf("a"));
        var paneIds = svc.CloseRoom(room);
        Assert.Contains("a", paneIds);
        Assert.Null(svc.GetRoot(room));
        Assert.Empty(svc.ToSnapshot(LayoutService.DefaultWorkspaceId, "d", "/d").Rooms);
    }

    [Fact]
    public void LoadSnapshot_KeepsOtherWorkspaces()
    {
        var svc = new LayoutService();
        svc.SetActiveWorkspace("wsA");
        svc.CreateRoom("A", Leaf("a"));

        var other = new WorkspaceSnapshot
        {
            Id = "wsB",
            Name = "B",
            ProjectDir = "/b",
            Rooms = new[] { new RoomSnapshot { Id = "rB", Name = "B-room", LayoutTree = Leaf("b") } },
        };
        svc.LoadSnapshot(other);

        Assert.Single(svc.ToSnapshot("wsA", "A", "/a").Rooms);
        Assert.Single(svc.ToSnapshot("wsB", "B", "/b").Rooms);
    }
}
