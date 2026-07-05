using System;
using System.Collections.Generic;
using System.Linq;
using Cove.Persistence;

namespace Cove.Engine.Layout;

public sealed class LayoutService
{
    private sealed class RoomState
    {
        public required string Name { get; set; }
        public required MosaicNode Root { get; set; }
        public string? ActivePaneId { get; set; }
        public string? ZoomedPaneId { get; set; }
    }

    private readonly Dictionary<string, RoomState> _rooms = new();
    private readonly object _sync = new();

    public string CreateRoom(string name, PaneLeaf firstLeaf)
    {
        lock (_sync)
        {
            var roomId = Guid.NewGuid().ToString("N");
            _rooms[roomId] = new RoomState
            {
                Name = name,
                Root = firstLeaf,
                ActivePaneId = firstLeaf.PaneId,
                ZoomedPaneId = null,
            };
            return roomId;
        }
    }

    public void SplitPane(string roomId, string targetPaneId, SplitOrientation orient, PaneLeaf newLeaf)
    {
        lock (_sync)
        {
            var room = GetRoomOrThrow(roomId);
            room.Root = MosaicOps.Split(room.Root, targetPaneId, orient, newLeaf);
            room.ActivePaneId = newLeaf.PaneId;
        }
    }

    public void ClosePane(string roomId, string paneId)
    {
        lock (_sync)
        {
            var room = GetRoomOrThrow(roomId);
            var next = MosaicOps.Close(room.Root, paneId);
            if (next is null)
            {
                _rooms.Remove(roomId);
                return;
            }

            room.Root = next;
            if (room.ActivePaneId == paneId)
            {
                var leaves = MosaicOps.Leaves(next);
                room.ActivePaneId = leaves.Count > 0 ? leaves[0].PaneId : null;
            }
        }
    }

    public void FocusPane(string roomId, string paneId)
    {
        lock (_sync)
        {
            var room = GetRoomOrThrow(roomId);
            room.ActivePaneId = paneId;
        }
    }

    public void CycleFocus(string roomId, int dir)
    {
        lock (_sync)
        {
            var room = GetRoomOrThrow(roomId);
            if (room.ActivePaneId is null)
                return;
            var next = MosaicOps.NextPane(room.Root, room.ActivePaneId, dir);
            if (next is not null)
                room.ActivePaneId = next;
        }
    }

    public void SetZoom(string roomId, string? paneId)
    {
        lock (_sync)
        {
            var room = GetRoomOrThrow(roomId);
            room.ZoomedPaneId = paneId;
        }
    }

    public void RenameRoom(string roomId, string name)
    {
        lock (_sync)
        {
            var room = GetRoomOrThrow(roomId);
            room.Name = name;
        }
    }

    public WorkspaceSnapshot ToSnapshot(string id, string name, string projectDir)
    {
        lock (_sync)
        {
            var rooms = new List<RoomSnapshot>(_rooms.Count);
            foreach (var kv in _rooms)
            {
                rooms.Add(new RoomSnapshot
                {
                    Id = kv.Key,
                    Name = kv.Value.Name,
                    LayoutTree = kv.Value.Root,
                    ZoomedPaneId = kv.Value.ZoomedPaneId,
                });
            }

            return new WorkspaceSnapshot
            {
                Id = id,
                Name = name,
                ProjectDir = projectDir,
                ActiveRoomId = rooms.Count > 0 ? rooms[0].Id : null,
                Rooms = rooms,
            };
        }
    }

    public void LoadSnapshot(WorkspaceSnapshot ws)
    {
        lock (_sync)
        {
            _rooms.Clear();
            foreach (var rs in ws.Rooms)
            {
                var leaves = MosaicOps.Leaves(rs.LayoutTree);
                _rooms[rs.Id] = new RoomState
                {
                    Name = rs.Name,
                    Root = rs.LayoutTree,
                    ActivePaneId = leaves.Count > 0 ? leaves[0].PaneId : null,
                    ZoomedPaneId = rs.ZoomedPaneId,
                };
            }
        }
    }

    public MosaicNode? GetRoot(string roomId)
    {
        lock (_sync)
        {
            return _rooms.TryGetValue(roomId, out var room) ? room.Root : null;
        }
    }

    public string? GetActive(string roomId)
    {
        lock (_sync)
        {
            return _rooms.TryGetValue(roomId, out var room) ? room.ActivePaneId : null;
        }
    }

    private RoomState GetRoomOrThrow(string roomId)
    {
        if (!_rooms.TryGetValue(roomId, out var room))
            throw new KeyNotFoundException($"Unknown room '{roomId}'.");
        return room;
    }
}
