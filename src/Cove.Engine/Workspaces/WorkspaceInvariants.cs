using Cove.Persistence;

namespace Cove.Engine.Workspaces;

public static class WorkspaceInvariants
{
    public static WorkspaceModel CloseRoom(WorkspaceModel model, string roomId, Func<string> newId)
    {
        var closed = model.Rooms.FirstOrDefault(r => r.Id == roomId);
        if (closed is null)
            return model;

        var rooms = model.Rooms.Where(r => r.Id != roomId).ToList();
        var panes = new Dictionary<string, PaneRecord>(model.Panes);
        foreach (var paneId in CollectPaneIds(closed.LayoutTree))
            panes.Remove(paneId);

        string? activeRoomId = model.ActiveRoomId;
        if (rooms.Count == 0)
        {
            var (room, pane) = Mint(WorkspaceModel.MainWingId, newId);
            rooms.Add(room);
            panes[pane.PaneId] = pane;
            activeRoomId = room.Id;
        }
        else if (activeRoomId == roomId)
        {
            activeRoomId = rooms[0].Id;
        }

        return model with { Rooms = rooms, Panes = panes, ActiveRoomId = activeRoomId };
    }

    public static WorkspaceModel RemoveWing(WorkspaceModel model, string wingId, Func<string> newId)
    {
        if (wingId == WorkspaceModel.MainWingId)
            return model;
        if (!model.Wings.Any(w => w.Id == wingId))
            return model;

        var rooms = model.Rooms
            .Select(r => r.WingId == wingId ? r with { WingId = WorkspaceModel.MainWingId } : r)
            .ToList();
        var wings = model.Wings.Where(w => w.Id != wingId).ToList();

        return model with { Wings = wings, Rooms = rooms };
    }

    public static WorkspaceModel SwitchWing(WorkspaceModel model, string wingId, Func<string> newId)
    {
        if (!model.Wings.Any(w => w.Id == wingId))
            return model;

        var wingRooms = model.Rooms.Where(r => r.WingId == wingId).ToList();
        if (wingRooms.Count == 0)
        {
            var (room, pane) = Mint(wingId, newId);
            var rooms = new List<Room>(model.Rooms) { room };
            var panes = new Dictionary<string, PaneRecord>(model.Panes) { [pane.PaneId] = pane };
            return model with { Rooms = rooms, Panes = panes, ActiveRoomId = room.Id };
        }

        return model with { ActiveRoomId = wingRooms[0].Id };
    }

    private static (Room Room, PaneRecord Pane) Mint(string wingId, Func<string> newId)
    {
        var paneId = newId();
        var roomId = newId();
        var room = new Room
        {
            Id = roomId,
            Name = "shell",
            WingId = wingId,
            ActivePaneId = paneId,
            LayoutTree = new PaneLeaf { PaneId = paneId },
        };
        return (room, new PaneRecord { PaneId = paneId });
    }

    public static IEnumerable<string> CollectPaneIds(MosaicNode node)
    {
        switch (node)
        {
            case PaneLeaf leaf:
                yield return leaf.PaneId;
                break;
            case SplitNode split:
                foreach (var id in CollectPaneIds(split.ChildA))
                    yield return id;
                foreach (var id in CollectPaneIds(split.ChildB))
                    yield return id;
                break;
        }
    }
}
