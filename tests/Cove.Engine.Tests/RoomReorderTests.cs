using Cove.Engine.Layout;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class RoomReorderTests
{
    private static PaneLeaf Leaf(string id) => new()
    {
        PaneId = id,
        Subtabs = new[] { new Subtab(id, PaneType.Terminal) },
    };

    [Fact]
    public void ReorderRooms_ChangesSnapshotOrder()
    {
        var svc = new LayoutService();
        var r1 = svc.CreateRoom("Room 1", Leaf("a"));
        var r2 = svc.CreateRoom("Room 2", Leaf("b"));
        var r3 = svc.CreateRoom("Room 3", Leaf("c"));

        svc.ReorderRooms(new[] { r3, r1, r2 });

        var snap = svc.ToSnapshot("ws", "demo", "/proj");
        Assert.Equal(r3, snap.Rooms[0].Id);
        Assert.Equal(r1, snap.Rooms[1].Id);
        Assert.Equal(r2, snap.Rooms[2].Id);
    }

    [Fact]
    public void ReorderRooms_PreservesAllRooms()
    {
        var svc = new LayoutService();
        var r1 = svc.CreateRoom("A", Leaf("a"));
        var r2 = svc.CreateRoom("B", Leaf("b"));

        svc.ReorderRooms(new[] { r2, r1 });

        var snap = svc.ToSnapshot("ws", "demo", "/proj");
        Assert.Equal(2, snap.Rooms.Count);
    }

    [Fact]
    public void ReorderRooms_IgnoresUnknownIds()
    {
        var svc = new LayoutService();
        var r1 = svc.CreateRoom("A", Leaf("a"));
        var r2 = svc.CreateRoom("B", Leaf("b"));

        svc.ReorderRooms(new[] { r2, r1, "nonexistent" });

        var snap = svc.ToSnapshot("ws", "demo", "/proj");
        Assert.Equal(2, snap.Rooms.Count);
        Assert.Equal(r2, snap.Rooms[0].Id);
        Assert.Equal(r1, snap.Rooms[1].Id);
    }

    [Fact]
    public void ReorderRooms_PartialListKeepsRemainingAtEnd()
    {
        var svc = new LayoutService();
        var r1 = svc.CreateRoom("A", Leaf("a"));
        var r2 = svc.CreateRoom("B", Leaf("b"));
        var r3 = svc.CreateRoom("C", Leaf("c"));

        svc.ReorderRooms(new[] { r2 });

        var snap = svc.ToSnapshot("ws", "demo", "/proj");
        Assert.Equal(r2, snap.Rooms[0].Id);
        Assert.Equal(3, snap.Rooms.Count);
    }

    [Fact]
    public void ReorderRooms_SurvivesRoundTrip()
    {
        var svc = new LayoutService();
        var r1 = svc.CreateRoom("A", Leaf("a"));
        var r2 = svc.CreateRoom("B", Leaf("b"));
        var r3 = svc.CreateRoom("C", Leaf("c"));
        svc.ReorderRooms(new[] { r3, r1, r2 });

        var snap = svc.ToSnapshot("ws", "demo", "/proj");
        var fresh = new LayoutService();
        fresh.LoadSnapshot(snap);
        var snap2 = fresh.ToSnapshot("ws", "demo", "/proj");

        Assert.Equal(r3, snap2.Rooms[0].Id);
        Assert.Equal(r1, snap2.Rooms[1].Id);
        Assert.Equal(r2, snap2.Rooms[2].Id);
    }

    [Fact]
    public void ClosePane_LastPane_KeepsRoomAsEmpty_PreservingOrder()
    {
        var svc = new LayoutService();
        var r1 = svc.CreateRoom("A", Leaf("a"));
        var r2 = svc.CreateRoom("B", Leaf("b"));
        var r3 = svc.CreateRoom("C", Leaf("c"));
        svc.ReorderRooms(new[] { r3, r1, r2 });

        svc.ClosePane(r1, "a");

        var snap = svc.ToSnapshot("ws", "demo", "/proj");
        Assert.Equal(3, snap.Rooms.Count);
        Assert.Equal(r3, snap.Rooms[0].Id);
        Assert.Equal(r1, snap.Rooms[1].Id);
        Assert.Equal(r2, snap.Rooms[2].Id);
        Assert.True(LayoutService.IsEmptyRoom(snap.Rooms[1].LayoutTree));
    }

    [Fact]
    public void CloseRoom_RemovesRoom_PreservingOrder()
    {
        var svc = new LayoutService();
        var r1 = svc.CreateRoom("A", Leaf("a"));
        var r2 = svc.CreateRoom("B", Leaf("b"));
        var r3 = svc.CreateRoom("C", Leaf("c"));
        svc.ReorderRooms(new[] { r3, r1, r2 });

        svc.CloseRoom(r1);

        var snap = svc.ToSnapshot("ws", "demo", "/proj");
        Assert.Equal(2, snap.Rooms.Count);
        Assert.Equal(r3, snap.Rooms[0].Id);
        Assert.Equal(r2, snap.Rooms[1].Id);
    }
}
