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
    public Action? OnChanged { get; set; }

    public string CreateRoom(string name, PaneLeaf firstLeaf)
    {
        string roomId;
        lock (_sync)
        {
            roomId = Guid.NewGuid().ToString("N");
            _rooms[roomId] = new RoomState
            {
                Name = name,
                Root = firstLeaf,
                ActivePaneId = firstLeaf.PaneId,
                ZoomedPaneId = null,
            };
        }
        OnChanged?.Invoke();
        return roomId;
    }

    public void SplitPane(string roomId, string targetPaneId, SplitOrientation orient, PaneLeaf newLeaf)
    {
        lock (_sync)
        {
            var room = GetRoomOrThrow(roomId);
            room.Root = MosaicOps.Split(room.Root, targetPaneId, orient, newLeaf);
            room.ActivePaneId = newLeaf.PaneId;
        }
        OnChanged?.Invoke();
    }

    public void AddSubtab(string roomId, string leafPaneId, string subtabDocId)
    {
        lock (_sync)
        {
            var room = GetRoomOrThrow(roomId);
            room.Root = MosaicOps.ReplaceLeaf(room.Root, leafPaneId, leaf => leaf with
            {
                Subtabs = Append(leaf.Subtabs, new Subtab(subtabDocId, PaneType.Terminal)),
                ActiveSubtab = leaf.Subtabs.Count,
            });
        }
        OnChanged?.Invoke();
    }

    public void ActivateSubtab(string roomId, string leafPaneId, int index)
    {
        lock (_sync)
        {
            var room = GetRoomOrThrow(roomId);
            room.Root = MosaicOps.ReplaceLeaf(room.Root, leafPaneId, leaf => leaf with
            {
                ActiveSubtab = Math.Clamp(index, 0, Math.Max(0, leaf.Subtabs.Count - 1)),
            });
        }
        OnChanged?.Invoke();
    }

    public void PromoteSubtab(string roomId, string leafPaneId, int subtabIndex, string newPaneId)
    {
        lock (_sync)
        {
            var room = GetRoomOrThrow(roomId);
            var leaf = MosaicOps.Find(room.Root, leafPaneId) ?? throw new KeyNotFoundException($"unknown pane {leafPaneId}");
            if (leaf.Subtabs.Count <= 1)
                throw new InvalidOperationException("cannot promote the only subtab");
            var idx = Math.Clamp(subtabIndex, 0, leaf.Subtabs.Count - 1);
            var promoted = leaf.Subtabs[idx];
            var remaining = RemoveAt(leaf.Subtabs, idx);
            var newLeaf = new PaneLeaf
            {
                PaneId = newPaneId,
                Subtabs = new[] { promoted },
                ActiveSubtab = 0,
            };
            room.Root = MosaicOps.ReplaceLeaf(room.Root, leafPaneId, _ => leaf with { Subtabs = remaining, ActiveSubtab = Math.Clamp(leaf.ActiveSubtab, 0, Math.Max(0, remaining.Length - 1)) });
            room.Root = MosaicOps.Split(room.Root, leafPaneId, SplitOrientation.Row, newLeaf);
            room.ActivePaneId = newPaneId;
        }
        OnChanged?.Invoke();
    }

    public void CenterDrop(string roomId, string sourceLeafPaneId, int subtabIndex, string targetLeafPaneId)
    {
        lock (_sync)
        {
            var room = GetRoomOrThrow(roomId);
            if (sourceLeafPaneId == targetLeafPaneId)
                throw new InvalidOperationException("cannot center-drop onto the same leaf");
            var source = MosaicOps.Find(room.Root, sourceLeafPaneId) ?? throw new KeyNotFoundException($"unknown pane {sourceLeafPaneId}");
            var target = MosaicOps.Find(room.Root, targetLeafPaneId) ?? throw new KeyNotFoundException($"unknown pane {targetLeafPaneId}");
            var idx = Math.Clamp(subtabIndex, 0, source.Subtabs.Count - 1);
            var moved = source.Subtabs[idx];
            var targetMerged = Append(target.Subtabs, moved);
            room.Root = MosaicOps.ReplaceLeaf(room.Root, targetLeafPaneId, _ => target with { Subtabs = targetMerged, ActiveSubtab = targetMerged.Length - 1 });
            if (source.Subtabs.Count <= 1)
            {
                var collapsed = MosaicOps.Close(room.Root, sourceLeafPaneId);
                if (collapsed is null)
                {
                    _rooms.Remove(roomId);
                }
                else
                {
                    room.Root = collapsed;
                    if (room.ActivePaneId == sourceLeafPaneId)
                        room.ActivePaneId = targetLeafPaneId;
                }
            }
            else
            {
                var sourceRemaining = RemoveAt(source.Subtabs, idx);
                room.Root = MosaicOps.ReplaceLeaf(room.Root, sourceLeafPaneId, _ => source with { Subtabs = sourceRemaining, ActiveSubtab = Math.Clamp(source.ActiveSubtab, 0, Math.Max(0, sourceRemaining.Length - 1)) });
            }
        }
        OnChanged?.Invoke();
    }

    private static Subtab[] RemoveAt(IReadOnlyList<Subtab> list, int index)
    {
        var arr = new Subtab[list.Count - 1];
        int j = 0;
        for (int i = 0; i < list.Count; i++)
            if (i != index)
                arr[j++] = list[i];
        return arr;
    }

    private static Subtab[] Append(IReadOnlyList<Subtab> list, Subtab item)
    {
        var arr = new Subtab[list.Count + 1];
        for (var i = 0; i < list.Count; i++)
            arr[i] = list[i];
        arr[^1] = item;
        return arr;
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
            }
            else
            {
                room.Root = next;
                if (room.ActivePaneId == paneId)
                {
                    var leaves = MosaicOps.Leaves(next);
                    room.ActivePaneId = leaves.Count > 0 ? leaves[0].PaneId : null;
                }
            }
        }
        OnChanged?.Invoke();
    }

    public void FocusPane(string roomId, string paneId)
    {
        lock (_sync)
        {
            var room = GetRoomOrThrow(roomId);
            room.ActivePaneId = paneId;
        }
        OnChanged?.Invoke();
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
        OnChanged?.Invoke();
    }

    public void SetZoom(string roomId, string? paneId)
    {
        lock (_sync)
        {
            var room = GetRoomOrThrow(roomId);
            room.ZoomedPaneId = paneId;
        }
        OnChanged?.Invoke();
    }

    public void RenameRoom(string roomId, string name)
    {
        lock (_sync)
        {
            var room = GetRoomOrThrow(roomId);
            room.Name = name;
        }
        OnChanged?.Invoke();
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
