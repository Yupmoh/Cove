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

    public static WorkspaceModel AddRoom(WorkspaceModel model, string wingId, string roomId, string paneId, string name)
    {
        var wing = model.Wings.Any(w => w.Id == wingId) ? wingId : WorkspaceModel.MainWingId;
        var room = new Room
        {
            Id = roomId,
            Name = name,
            WingId = wing,
            ActivePaneId = paneId,
            LayoutTree = new PaneLeaf { PaneId = paneId },
        };
        var rooms = new List<Room>(model.Rooms) { room };
        var panes = new Dictionary<string, PaneRecord>(model.Panes) { [paneId] = new PaneRecord { PaneId = paneId } };
        return model with { Rooms = rooms, Panes = panes, ActiveRoomId = roomId };
    }

    public static WorkspaceModel RenameRoom(WorkspaceModel model, string roomId, string name)
        => model with { Rooms = model.Rooms.Select(r => r.Id == roomId ? r with { Name = name } : r).ToList() };

    public static WorkspaceModel SetRoomPinned(WorkspaceModel model, string roomId, bool pinned)
        => model with { Rooms = model.Rooms.Select(r => r.Id == roomId ? r with { Pinned = pinned } : r).ToList() };

    public static WorkspaceModel MoveRoomToWing(WorkspaceModel model, string roomId, string wingId)
    {
        if (!model.Wings.Any(w => w.Id == wingId))
            return model;
        return model with { Rooms = model.Rooms.Select(r => r.Id == roomId ? r with { WingId = wingId } : r).ToList() };
    }

    public static WorkspaceModel SwitchRoom(WorkspaceModel model, string roomId)
        => model.Rooms.Any(r => r.Id == roomId) ? model with { ActiveRoomId = roomId } : model;

    public static WorkspaceModel AddWing(WorkspaceModel model, string wingId, string name)
    {
        if (model.Wings.Any(w => w.Id == wingId))
            return model;
        return model with { Wings = new List<Wing>(model.Wings) { new Wing { Id = wingId, Name = name } } };
    }

    public static WorkspaceModel RenameWing(WorkspaceModel model, string wingId, string name)
        => model with { Wings = model.Wings.Select(w => w.Id == wingId ? w with { Name = name } : w).ToList() };

    public static WorkspaceModel ReorderWings(WorkspaceModel model, IReadOnlyList<string> orderedIds)
    {
        var known = new HashSet<string>(model.Wings.Select(w => w.Id), System.StringComparer.Ordinal);
        var next = new List<Wing>();
        foreach (var id in orderedIds)
            if (known.Contains(id))
                next.Add(model.Wings.First(w => w.Id == id));
        foreach (var w in model.Wings)
            if (!next.Exists(x => x.Id == w.Id))
                next.Add(w);
        return model with { Wings = next };
    }

    public static WorkspaceModel SetWingIcon(WorkspaceModel model, string wingId, WorkspaceIcon? icon)
        => model with { Wings = model.Wings.Select(w => w.Id == wingId ? w with { Icon = icon } : w).ToList() };

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
