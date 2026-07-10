using System;
using System.Collections.Generic;
using System.Linq;
using Cove.Persistence;

namespace Cove.Engine.Layout;

public sealed class LayoutService
{
    public const string DefaultWorkspaceId = "default";

    private sealed class RoomState
    {
        public required string Name { get; set; }
        public required MosaicNode Root { get; set; }
        public string? ActivePaneId { get; set; }
        public string? ZoomedPaneId { get; set; }
    }

    private sealed class Bucket
    {
        public readonly Dictionary<string, RoomState> Rooms = new(StringComparer.Ordinal);
        public readonly List<string> Order = new();
        public string? ActiveRoomId;
    }

    private readonly Dictionary<string, Bucket> _buckets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _roomToWorkspace = new(StringComparer.Ordinal);
    private string _activeWorkspaceId = DefaultWorkspaceId;
    private readonly object _sync = new();
    public Action? OnChanged { get; set; }

    public LayoutService()
    {
        _buckets[DefaultWorkspaceId] = new Bucket();
    }

    public string ActiveWorkspaceId
    {
        get { lock (_sync) return _activeWorkspaceId; }
    }

    public IReadOnlyList<string> WorkspaceIds
    {
        get { lock (_sync) return _buckets.Keys.ToList(); }
    }

    public void SetActiveWorkspace(string workspaceId)
    {
        lock (_sync)
        {
            EnsureBucket(workspaceId);
            _activeWorkspaceId = workspaceId;
        }
        OnChanged?.Invoke();
    }

    public void EnsureWorkspace(string workspaceId)
    {
        lock (_sync)
            EnsureBucket(workspaceId);
    }

    public IReadOnlyList<string> RemoveWorkspace(string workspaceId)
    {
        var paneIds = new List<string>();
        lock (_sync)
        {
            if (!_buckets.TryGetValue(workspaceId, out var bucket))
                return paneIds;
            foreach (var roomId in bucket.Order)
            {
                _roomToWorkspace.Remove(roomId);
                if (bucket.Rooms.TryGetValue(roomId, out var rs))
                    foreach (var leaf in MosaicOps.Leaves(rs.Root))
                        if (!IsEmptyLeaf(leaf))
                            paneIds.Add(leaf.PaneId);
            }
            _buckets.Remove(workspaceId);
            if (_activeWorkspaceId == workspaceId)
                _activeWorkspaceId = _buckets.Keys.FirstOrDefault() ?? DefaultWorkspaceId;
            EnsureBucket(_activeWorkspaceId);
        }
        OnChanged?.Invoke();
        return paneIds;
    }

    public string CreateRoom(string name, PaneLeaf firstLeaf)
    {
        string roomId;
        lock (_sync)
        {
            var bucket = ActiveBucket();
            roomId = Guid.NewGuid().ToString("N");
            bucket.Rooms[roomId] = new RoomState
            {
                Name = name,
                Root = firstLeaf,
                ActivePaneId = firstLeaf.PaneId,
                ZoomedPaneId = null,
            };
            bucket.Order.Add(roomId);
            bucket.ActiveRoomId = roomId;
            _roomToWorkspace[roomId] = _activeWorkspaceId;
        }
        OnChanged?.Invoke();
        return roomId;
    }

    public void SplitPane(string roomId, string targetPaneId, SplitOrientation orient, PaneLeaf newLeaf)
    {
        lock (_sync)
        {
            var (bucket, room) = GetRoomOrThrow(roomId);
            room.Root = MosaicOps.Split(room.Root, targetPaneId, orient, newLeaf);
            room.ActivePaneId = newLeaf.PaneId;
            bucket.ActiveRoomId = roomId;
        }
        OnChanged?.Invoke();
    }

    public void ReplacePane(string roomId, string targetPaneId, PaneLeaf newLeaf)
    {
        lock (_sync)
        {
            var (bucket, room) = GetRoomOrThrow(roomId);
            room.Root = MosaicOps.ReplaceLeaf(room.Root, targetPaneId, _ => newLeaf);
            room.ActivePaneId = newLeaf.PaneId;
            bucket.ActiveRoomId = roomId;
        }
        OnChanged?.Invoke();
    }

    public void AddSubtab(string roomId, string leafPaneId, string subtabDocId)
    {
        lock (_sync)
        {
            var (_, room) = GetRoomOrThrow(roomId);
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
            var (_, room) = GetRoomOrThrow(roomId);
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
            var (_, room) = GetRoomOrThrow(roomId);
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
            var (bucket, room) = GetRoomOrThrow(roomId);
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
                    room.Root = MakeEmptyLeaf();
                    room.ActivePaneId = ((PaneLeaf)room.Root).PaneId;
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
            _ = bucket;
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

    public void MovePane(string roomId, string paneId, string targetPaneId, SplitOrientation orientation, int dir)
    {
        lock (_sync)
        {
            var (_, room) = GetRoomOrThrow(roomId);
            if (paneId == targetPaneId)
                throw new InvalidOperationException("cannot move a pane onto itself");
            var source = MosaicOps.Find(room.Root, paneId) ?? throw new KeyNotFoundException($"unknown pane {paneId}");
            if (MosaicOps.Find(room.Root, targetPaneId) is null)
                throw new KeyNotFoundException($"unknown pane {targetPaneId}");
            var without = MosaicOps.Close(room.Root, paneId) ?? throw new InvalidOperationException("cannot move the only pane in a room");
            room.Root = MosaicOps.Split(without, targetPaneId, orientation, source, before: dir < 0);
            room.ActivePaneId = paneId;
            room.ZoomedPaneId = null;
        }
        OnChanged?.Invoke();
    }

    public void MovePaneToRoom(string paneId, string targetRoomId)
    {
        lock (_sync)
        {
            var (_, targetRoom) = GetRoomOrThrow(targetRoomId);
            RoomState? sourceRoom = null;
            foreach (var bucket in _buckets.Values)
            {
                foreach (var rs in bucket.Rooms.Values)
                {
                    if (MosaicOps.Find(rs.Root, paneId) is not null)
                    {
                        sourceRoom = rs;
                        break;
                    }
                }
                if (sourceRoom is not null)
                    break;
            }
            if (sourceRoom is null)
                throw new KeyNotFoundException($"unknown pane {paneId}");
            if (ReferenceEquals(sourceRoom, targetRoom))
                throw new InvalidOperationException("pane is already in the target room");
            var moved = MosaicOps.Find(sourceRoom.Root, paneId)!;
            var without = MosaicOps.Close(sourceRoom.Root, paneId);
            if (without is null)
            {
                sourceRoom.Root = MakeEmptyLeaf();
                sourceRoom.ActivePaneId = ((PaneLeaf)sourceRoom.Root).PaneId;
                sourceRoom.ZoomedPaneId = null;
            }
            else
            {
                sourceRoom.Root = without;
                if (sourceRoom.ActivePaneId == paneId)
                {
                    var leaves = MosaicOps.Leaves(without);
                    sourceRoom.ActivePaneId = leaves.Count > 0 ? leaves[0].PaneId : null;
                }
            }
            if (IsEmptyRoom(targetRoom.Root))
                targetRoom.Root = moved;
            else
                targetRoom.Root = new SplitNode { Orientation = SplitOrientation.Row, Ratio = 0.5, ChildA = targetRoom.Root, ChildB = moved };
            targetRoom.ActivePaneId = paneId;
            targetRoom.ZoomedPaneId = null;
        }
        OnChanged?.Invoke();
    }

    public void ClosePane(string roomId, string paneId)
    {
        lock (_sync)
        {
            var (_, room) = GetRoomOrThrow(roomId);
            var next = MosaicOps.Close(room.Root, paneId);
            if (next is null)
            {
                room.Root = MakeEmptyLeaf();
                room.ActivePaneId = ((PaneLeaf)room.Root).PaneId;
                room.ZoomedPaneId = null;
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

    public IReadOnlyList<string> CloseRoom(string roomId)
    {
        var paneIds = new List<string>();
        lock (_sync)
        {
            if (!_roomToWorkspace.TryGetValue(roomId, out var wsId) || !_buckets.TryGetValue(wsId, out var bucket))
                return paneIds;
            if (bucket.Rooms.TryGetValue(roomId, out var rs))
                foreach (var leaf in MosaicOps.Leaves(rs.Root))
                    if (!IsEmptyLeaf(leaf))
                        paneIds.Add(leaf.PaneId);
            bucket.Rooms.Remove(roomId);
            bucket.Order.Remove(roomId);
            _roomToWorkspace.Remove(roomId);
            if (bucket.ActiveRoomId == roomId)
                bucket.ActiveRoomId = bucket.Order.Count > 0 ? bucket.Order[0] : null;
        }
        OnChanged?.Invoke();
        return paneIds;
    }

    public void FocusPane(string roomId, string paneId)
    {
        lock (_sync)
        {
            var (bucket, room) = GetRoomOrThrow(roomId);
            room.ActivePaneId = paneId;
            bucket.ActiveRoomId = roomId;
        }
        OnChanged?.Invoke();
    }

    public void CycleFocus(string roomId, int dir)
    {
        lock (_sync)
        {
            var (_, room) = GetRoomOrThrow(roomId);
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
            var (_, room) = GetRoomOrThrow(roomId);
            room.ZoomedPaneId = paneId;
        }
        OnChanged?.Invoke();
    }

    public void RenameRoom(string roomId, string name)
    {
        lock (_sync)
        {
            var (_, room) = GetRoomOrThrow(roomId);
            room.Name = name;
        }
        OnChanged?.Invoke();
    }

    public void ReorderRooms(IReadOnlyList<string> orderedRoomIds)
    {
        lock (_sync)
        {
            var bucket = ActiveBucket();
            var known = new HashSet<string>(bucket.Rooms.Keys, StringComparer.Ordinal);
            var next = new List<string>(bucket.Rooms.Count);
            foreach (var id in orderedRoomIds)
            {
                if (known.Remove(id))
                    next.Add(id);
            }
            foreach (var id in bucket.Order)
            {
                if (known.Remove(id))
                    next.Add(id);
            }
            bucket.Order.Clear();
            bucket.Order.AddRange(next);
        }
        OnChanged?.Invoke();
    }

    public WorkspaceSnapshot ToSnapshot(string id, string name, string projectDir)
    {
        lock (_sync)
        {
            var bucket = _buckets.TryGetValue(id, out var b) ? b : ActiveBucket();
            var rooms = new List<RoomSnapshot>(bucket.Rooms.Count);
            foreach (var roomId in bucket.Order)
            {
                if (!bucket.Rooms.TryGetValue(roomId, out var rs))
                    continue;
                rooms.Add(new RoomSnapshot
                {
                    Id = roomId,
                    Name = rs.Name,
                    LayoutTree = rs.Root,
                    ZoomedPaneId = rs.ZoomedPaneId,
                });
            }

            return new WorkspaceSnapshot
            {
                Id = id,
                Name = name,
                ProjectDir = projectDir,
                ActiveRoomId = bucket.ActiveRoomId ?? (rooms.Count > 0 ? rooms[0].Id : null),
                Rooms = rooms,
            };
        }
    }

    public void LoadSnapshot(WorkspaceSnapshot ws)
    {
        lock (_sync)
        {
            var bucket = new Bucket();
            foreach (var rs in ws.Rooms)
            {
                var leaves = MosaicOps.Leaves(rs.LayoutTree);
                bucket.Rooms[rs.Id] = new RoomState
                {
                    Name = rs.Name,
                    Root = rs.LayoutTree,
                    ActivePaneId = leaves.Count > 0 ? leaves[0].PaneId : null,
                    ZoomedPaneId = rs.ZoomedPaneId,
                };
                bucket.Order.Add(rs.Id);
                _roomToWorkspace[rs.Id] = ws.Id;
            }
            bucket.ActiveRoomId = ws.ActiveRoomId ?? (bucket.Order.Count > 0 ? bucket.Order[0] : null);
            _buckets[ws.Id] = bucket;
            _activeWorkspaceId = ws.Id;
        }
    }

    public MosaicNode? GetRoot(string roomId)
    {
        lock (_sync)
        {
            if (!_roomToWorkspace.TryGetValue(roomId, out var wsId) || !_buckets.TryGetValue(wsId, out var bucket))
                return null;
            return bucket.Rooms.TryGetValue(roomId, out var room) ? room.Root : null;
        }
    }

    public string? GetActive(string roomId)
    {
        lock (_sync)
        {
            if (!_roomToWorkspace.TryGetValue(roomId, out var wsId) || !_buckets.TryGetValue(wsId, out var bucket))
                return null;
            return bucket.Rooms.TryGetValue(roomId, out var room) ? room.ActivePaneId : null;
        }
    }

    public string? FocusedPaneId()
    {
        lock (_sync)
        {
            var bucket = ActiveBucket();
            var roomId = bucket.ActiveRoomId ?? (bucket.Order.Count > 0 ? bucket.Order[0] : null);
            if (roomId is null || !bucket.Rooms.TryGetValue(roomId, out var room))
                return null;
            return room.ActivePaneId;
        }
    }

    public IReadOnlyList<string> LeafPaneIds(string workspaceId)
    {
        var ids = new List<string>();
        lock (_sync)
        {
            if (!_buckets.TryGetValue(workspaceId, out var bucket))
                return ids;
            foreach (var roomId in bucket.Order)
                if (bucket.Rooms.TryGetValue(roomId, out var rs))
                    foreach (var leaf in MosaicOps.Leaves(rs.Root))
                        if (!IsEmptyLeaf(leaf))
                            ids.Add(leaf.PaneId);
        }
        return ids;
    }

    public static bool IsEmptyRoom(MosaicNode node) => node is PaneLeaf leaf && IsEmptyLeaf(leaf);

    private static bool IsEmptyLeaf(PaneLeaf leaf)
        => leaf.Subtabs.Count == 0 || leaf.Subtabs.All(s => s.PaneType == PaneType.Empty);

    private static PaneLeaf MakeEmptyLeaf()
    {
        var id = Guid.NewGuid().ToString("N");
        return new PaneLeaf { PaneId = id, Subtabs = new[] { new Subtab(id, PaneType.Empty) } };
    }

    private Bucket ActiveBucket() => EnsureBucket(_activeWorkspaceId);

    private Bucket EnsureBucket(string workspaceId)
    {
        if (!_buckets.TryGetValue(workspaceId, out var bucket))
        {
            bucket = new Bucket();
            _buckets[workspaceId] = bucket;
        }
        return bucket;
    }

    private (Bucket Bucket, RoomState Room) GetRoomOrThrow(string roomId)
    {
        if (!_roomToWorkspace.TryGetValue(roomId, out var wsId) || !_buckets.TryGetValue(wsId, out var bucket) || !bucket.Rooms.TryGetValue(roomId, out var room))
            throw new KeyNotFoundException($"Unknown room '{roomId}'.");
        return (bucket, room);
    }
}
