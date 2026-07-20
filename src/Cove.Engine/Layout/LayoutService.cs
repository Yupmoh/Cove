using System;
using System.Collections.Generic;
using System.Linq;
using Cove.Persistence;

namespace Cove.Engine.Layout;

public sealed class LayoutService
{
    public const string DefaultBayId = "default";
    public const string MainWingId = "main";

    public readonly record struct ShoreView(string Id, string Name, string WingId, bool Pinned, bool Active);
    public readonly record struct WingView(string Id, string Name, string? IconKind, string? IconValue);

    private sealed class WingState
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public string? IconKind { get; set; }
        public string? IconValue { get; set; }
    }

    private sealed class ShoreState
    {
        public required string Name { get; set; }
        public required MosaicNode Root { get; set; }
        public string? ActiveNookId { get; set; }
        public string? ZoomedNookId { get; set; }
        public string WingId { get; set; } = MainWingId;
        public bool Pinned { get; set; }
    }

    private sealed class Bucket
    {
        public readonly Dictionary<string, ShoreState> Shores = new(StringComparer.Ordinal);
        public readonly List<string> Order = new();
        public string? ActiveShoreId;
        public readonly List<WingState> Wings = new() { new WingState { Id = MainWingId, Name = "main" } };
        public string? ActiveWingId = MainWingId;
        public string? FocusedNookId;
    }

    private sealed class WorkspaceAggregate
    {
        public readonly Dictionary<string, Bucket> Bays = new(StringComparer.Ordinal);
        public readonly Dictionary<string, string> ShoreOwners = new(StringComparer.Ordinal);
        public readonly List<string> OpenBayIds = new();
        public string ActiveBayId = DefaultBayId;
    }

    private readonly WorkspaceAggregate _workspace = new();
    private readonly object _sync = new();
    public Action? OnChanged { get; set; }
    public Action<string>? OnBayChanged { get; set; }

    public LayoutService()
    {
        _workspace.Bays[DefaultBayId] = new Bucket();
    }

    public string ActiveBayId
    {
        get { lock (_sync) return _workspace.ActiveBayId; }
    }

    public IReadOnlyList<string> BayIds
    {
        get { lock (_sync) return _workspace.OpenBayIds.ToArray(); }
    }

    public void SetActiveBay(string bayId)
        => SetActiveBay(bayId, true);

    internal void SetActiveBay(string bayId, bool notify)
    {
        lock (_sync)
        {
            EnsureBucket(bayId);
            AppendOpenBay(bayId);
            _workspace.ActiveBayId = bayId;
        }
        if (notify)
            OnChanged?.Invoke();
    }

    public void EnsureBay(string bayId)
    {
        lock (_sync)
        {
            EnsureBucket(bayId);
            AppendOpenBay(bayId);
        }
    }

    public IReadOnlyList<string> RemoveBay(string bayId)
        => RemoveBay(bayId, true);

    internal IReadOnlyList<string> RemoveBay(string bayId, bool notify)
    {
        var nookIds = new List<string>();
        lock (_sync)
        {
            if (!_workspace.Bays.TryGetValue(bayId, out var bucket))
                return nookIds;
            foreach (var shoreId in bucket.Order)
            {
                _workspace.ShoreOwners.Remove(shoreId);
                if (bucket.Shores.TryGetValue(shoreId, out var rs))
                    foreach (var leaf in MosaicOps.Leaves(rs.Root))
                        if (!IsEmptyLeaf(leaf))
                            nookIds.Add(leaf.NookId);
            }
            _workspace.Bays.Remove(bayId);
            _workspace.OpenBayIds.Remove(bayId);
            if (_workspace.ActiveBayId == bayId)
                _workspace.ActiveBayId = _workspace.OpenBayIds.FirstOrDefault() ?? DefaultBayId;
            EnsureBucket(_workspace.ActiveBayId);
        }
        if (notify)
            OnChanged?.Invoke();
        return nookIds;
    }

    internal IReadOnlyList<string> OpenBayIds
    {
        get
        {
            lock (_sync)
                return _workspace.OpenBayIds.ToArray();
        }
    }

    internal void RegisterBay(string bayId, bool activate)
    {
        lock (_sync)
        {
            EnsureBucket(bayId);
            AppendOpenBay(bayId);
            if (activate || _workspace.OpenBayIds.Count == 1)
                _workspace.ActiveBayId = bayId;
        }
    }

    internal void ReorderBays(IReadOnlyList<string> orderedIds)
    {
        lock (_sync)
        {
            var known = new HashSet<string>(_workspace.OpenBayIds, StringComparer.Ordinal);
            var next = new List<string>(_workspace.OpenBayIds.Count);
            foreach (var id in orderedIds)
                if (known.Contains(id) && !next.Contains(id, StringComparer.Ordinal))
                    next.Add(id);
            foreach (var id in _workspace.OpenBayIds)
                if (!next.Contains(id, StringComparer.Ordinal))
                    next.Add(id);
            _workspace.OpenBayIds.Clear();
            _workspace.OpenBayIds.AddRange(next);
        }
    }

    public string CreateShore(string name, NookLeaf firstLeaf)
    {
        string shoreId;
        lock (_sync)
        {
            var bucket = ActiveBucket();
            AppendOpenBay(_workspace.ActiveBayId);
            shoreId = Guid.NewGuid().ToString("N");
            bucket.Shores[shoreId] = new ShoreState
            {
                Name = name,
                Root = firstLeaf,
                ActiveNookId = firstLeaf.NookId,
                ZoomedNookId = null,
                WingId = bucket.ActiveWingId ?? MainWingId,
            };
            bucket.Order.Add(shoreId);
            bucket.ActiveShoreId = shoreId;
            _workspace.ShoreOwners[shoreId] = _workspace.ActiveBayId;
        }
        OnChanged?.Invoke();
        return shoreId;
    }

    public void SplitNook(
        string shoreId,
        string targetNookId,
        SplitOrientation orient,
        NookLeaf newLeaf,
        bool before = false)
    {
        lock (_sync)
        {
            var (bucket, shore) = GetShoreOrThrow(shoreId);
            shore.Root = MosaicOps.Split(
                shore.Root,
                targetNookId,
                orient,
                newLeaf,
                before: before);
            shore.ActiveNookId = newLeaf.NookId;
            bucket.ActiveShoreId = shoreId;
        }
        OnChanged?.Invoke();
    }

    public void ReplaceNook(string shoreId, string targetNookId, NookLeaf newLeaf)
    {
        lock (_sync)
        {
            var (bucket, shore) = GetShoreOrThrow(shoreId);
            shore.Root = MosaicOps.ReplaceLeaf(shore.Root, targetNookId, _ => newLeaf);
            shore.ActiveNookId = newLeaf.NookId;
            bucket.ActiveShoreId = shoreId;
        }
        OnChanged?.Invoke();
    }

    public void AddSubtab(string shoreId, string leafNookId, string subtabDocId)
    {
        lock (_sync)
        {
            var (_, shore) = GetShoreOrThrow(shoreId);
            shore.Root = MosaicOps.ReplaceLeaf(shore.Root, leafNookId, leaf => leaf with
            {
                Subtabs = Append(leaf.Subtabs, new Subtab(subtabDocId, NookType.Terminal)),
                ActiveSubtab = leaf.Subtabs.Count,
            });
        }
        OnChanged?.Invoke();
    }

    public void ActivateSubtab(string shoreId, string leafNookId, int index)
    {
        lock (_sync)
        {
            var (_, shore) = GetShoreOrThrow(shoreId);
            shore.Root = MosaicOps.ReplaceLeaf(shore.Root, leafNookId, leaf => leaf with
            {
                ActiveSubtab = Math.Clamp(index, 0, Math.Max(0, leaf.Subtabs.Count - 1)),
            });
        }
        OnChanged?.Invoke();
    }

    public void PromoteSubtab(string shoreId, string leafNookId, int subtabIndex, string newNookId)
    {
        lock (_sync)
        {
            var (_, shore) = GetShoreOrThrow(shoreId);
            var leaf = MosaicOps.Find(shore.Root, leafNookId) ?? throw new KeyNotFoundException($"unknown nook {leafNookId}");
            if (leaf.Subtabs.Count <= 1)
                throw new InvalidOperationException("cannot promote the only subtab");
            var idx = Math.Clamp(subtabIndex, 0, leaf.Subtabs.Count - 1);
            var promoted = leaf.Subtabs[idx];
            var remaining = RemoveAt(leaf.Subtabs, idx);
            var newLeaf = new NookLeaf
            {
                NookId = newNookId,
                Subtabs = new[] { promoted },
                ActiveSubtab = 0,
            };
            shore.Root = MosaicOps.ReplaceLeaf(shore.Root, leafNookId, _ => leaf with { Subtabs = remaining, ActiveSubtab = Math.Clamp(leaf.ActiveSubtab, 0, Math.Max(0, remaining.Length - 1)) });
            shore.Root = MosaicOps.Split(shore.Root, leafNookId, SplitOrientation.Row, newLeaf);
            shore.ActiveNookId = newNookId;
        }
        OnChanged?.Invoke();
    }

    public void CenterDrop(string shoreId, string sourceLeafNookId, int subtabIndex, string targetLeafNookId)
    {
        lock (_sync)
        {
            var (bucket, shore) = GetShoreOrThrow(shoreId);
            if (sourceLeafNookId == targetLeafNookId)
                throw new InvalidOperationException("cannot center-drop onto the same leaf");
            var source = MosaicOps.Find(shore.Root, sourceLeafNookId) ?? throw new KeyNotFoundException($"unknown nook {sourceLeafNookId}");
            var target = MosaicOps.Find(shore.Root, targetLeafNookId) ?? throw new KeyNotFoundException($"unknown nook {targetLeafNookId}");
            var idx = Math.Clamp(subtabIndex, 0, source.Subtabs.Count - 1);
            var moved = source.Subtabs[idx];
            var targetMerged = Append(target.Subtabs, moved);
            shore.Root = MosaicOps.ReplaceLeaf(shore.Root, targetLeafNookId, _ => target with { Subtabs = targetMerged, ActiveSubtab = targetMerged.Length - 1 });
            if (source.Subtabs.Count <= 1)
            {
                var collapsed = MosaicOps.Close(shore.Root, sourceLeafNookId);
                if (collapsed is null)
                {
                    shore.Root = MakeEmptyLeaf();
                    shore.ActiveNookId = ((NookLeaf)shore.Root).NookId;
                }
                else
                {
                    shore.Root = collapsed;
                    if (shore.ActiveNookId == sourceLeafNookId)
                        shore.ActiveNookId = targetLeafNookId;
                }
            }
            else
            {
                var sourceRemaining = RemoveAt(source.Subtabs, idx);
                shore.Root = MosaicOps.ReplaceLeaf(shore.Root, sourceLeafNookId, _ => source with { Subtabs = sourceRemaining, ActiveSubtab = Math.Clamp(source.ActiveSubtab, 0, Math.Max(0, sourceRemaining.Length - 1)) });
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

    public void MoveNook(string shoreId, string nookId, string targetNookId, SplitOrientation orientation, int dir)
    {
        lock (_sync)
        {
            var (_, shore) = GetShoreOrThrow(shoreId);
            if (nookId == targetNookId)
                throw new InvalidOperationException("cannot move a nook onto itself");
            var source = MosaicOps.Find(shore.Root, nookId) ?? throw new KeyNotFoundException($"unknown nook {nookId}");
            if (MosaicOps.Find(shore.Root, targetNookId) is null)
                throw new KeyNotFoundException($"unknown nook {targetNookId}");
            var without = MosaicOps.Close(shore.Root, nookId) ?? throw new InvalidOperationException("cannot move the only nook in a shore");
            shore.Root = MosaicOps.Split(without, targetNookId, orientation, source, before: dir < 0);
            shore.ActiveNookId = nookId;
            shore.ZoomedNookId = null;
        }
        OnChanged?.Invoke();
    }

    public void MoveNookToShore(string nookId, string targetShoreId)
    {
        lock (_sync)
        {
            var (_, targetShore) = GetShoreOrThrow(targetShoreId);
            ShoreState? sourceShore = null;
            foreach (var bucket in _workspace.Bays.Values)
            {
                foreach (var rs in bucket.Shores.Values)
                {
                    if (MosaicOps.Find(rs.Root, nookId) is not null)
                    {
                        sourceShore = rs;
                        break;
                    }
                }
                if (sourceShore is not null)
                    break;
            }
            if (sourceShore is null)
                throw new KeyNotFoundException($"unknown nook {nookId}");
            if (ReferenceEquals(sourceShore, targetShore))
                throw new InvalidOperationException("nook is already in the target shore");
            var moved = MosaicOps.Find(sourceShore.Root, nookId)!;
            var without = MosaicOps.Close(sourceShore.Root, nookId);
            if (without is null)
            {
                sourceShore.Root = MakeEmptyLeaf();
                sourceShore.ActiveNookId = ((NookLeaf)sourceShore.Root).NookId;
                sourceShore.ZoomedNookId = null;
            }
            else
            {
                sourceShore.Root = without;
                if (sourceShore.ActiveNookId == nookId)
                {
                    var leaves = MosaicOps.Leaves(without);
                    sourceShore.ActiveNookId = leaves.Count > 0 ? leaves[0].NookId : null;
                }
            }
            if (IsEmptyShore(targetShore.Root))
                targetShore.Root = moved;
            else
                targetShore.Root = new SplitNode { Orientation = SplitOrientation.Row, Ratio = 0.5, ChildA = targetShore.Root, ChildB = moved };
            targetShore.ActiveNookId = nookId;
            targetShore.ZoomedNookId = null;
        }
        OnChanged?.Invoke();
    }

    public void CloseNook(string shoreId, string nookId)
    {
        lock (_sync)
        {
            var (bucket, shore) = GetShoreOrThrow(shoreId);
            var next = MosaicOps.Close(shore.Root, nookId);
            if (next is null)
            {
                bucket.Shores.Remove(shoreId);
                bucket.Order.Remove(shoreId);
                _workspace.ShoreOwners.Remove(shoreId);
                if (bucket.ActiveShoreId == shoreId)
                    bucket.ActiveShoreId = bucket.Order.Count > 0 ? bucket.Order[0] : null;
            }
            else
            {
                shore.Root = next;
                if (shore.ActiveNookId == nookId)
                {
                    var leaves = MosaicOps.Leaves(next);
                    shore.ActiveNookId = leaves.Count > 0 ? leaves[0].NookId : null;
                }
            }
            if (bucket.FocusedNookId == nookId)
            {
                bucket.FocusedNookId =
                    bucket.ActiveShoreId is { } activeShoreId
                    && bucket.Shores.TryGetValue(activeShoreId, out var activeShore)
                        ? activeShore.ActiveNookId
                        : null;
            }
        }
        OnChanged?.Invoke();
    }

    public IReadOnlyList<string> CloseShore(string shoreId)
    {
        var nookIds = new List<string>();
        string? ownerBay = null;
        lock (_sync)
        {
            if (!_workspace.ShoreOwners.TryGetValue(shoreId, out var wsId) || !_workspace.Bays.TryGetValue(wsId, out var bucket))
                return nookIds;
            ownerBay = wsId;
            if (bucket.Shores.TryGetValue(shoreId, out var rs))
                foreach (var leaf in MosaicOps.Leaves(rs.Root))
                    if (!IsEmptyLeaf(leaf))
                        nookIds.Add(leaf.NookId);
            bucket.Shores.Remove(shoreId);
            bucket.Order.Remove(shoreId);
            _workspace.ShoreOwners.Remove(shoreId);
            if (bucket.ActiveShoreId == shoreId)
                bucket.ActiveShoreId = bucket.Order.Count > 0 ? bucket.Order[0] : null;
        }
        if (ownerBay is not null)
            OnBayChanged?.Invoke(ownerBay);
        else
            OnChanged?.Invoke();
        return nookIds;
    }

    public void FocusNook(string shoreId, string nookId)
    {
        string? ownerBay;
        lock (_sync)
        {
            var (bucket, shore) = GetShoreOrThrow(shoreId);
            shore.ActiveNookId = nookId;
            bucket.ActiveShoreId = shoreId;
            bucket.FocusedNookId = nookId;
            ownerBay = _workspace.ShoreOwners.TryGetValue(shoreId, out var wsId) ? wsId : null;
        }
        if (ownerBay is not null)
            OnBayChanged?.Invoke(ownerBay);
        else
            OnChanged?.Invoke();
    }

    public void CycleFocus(string shoreId, int dir)
    {
        lock (_sync)
        {
            var (_, shore) = GetShoreOrThrow(shoreId);
            if (shore.ActiveNookId is null)
                return;
            var next = MosaicOps.NextNook(shore.Root, shore.ActiveNookId, dir);
            if (next is not null)
                shore.ActiveNookId = next;
        }
        OnChanged?.Invoke();
    }

    public void SetZoom(string shoreId, string? nookId)
    {
        lock (_sync)
        {
            var (_, shore) = GetShoreOrThrow(shoreId);
            shore.ZoomedNookId = nookId;
        }
        OnChanged?.Invoke();
    }

    public void RenameShore(string shoreId, string name)
    {
        string? ownerBay;
        lock (_sync)
        {
            var (_, shore) = GetShoreOrThrow(shoreId);
            shore.Name = name;
            ownerBay = _workspace.ShoreOwners.TryGetValue(shoreId, out var wsId) ? wsId : null;
        }
        if (ownerBay is not null)
            OnBayChanged?.Invoke(ownerBay);
        else
            OnChanged?.Invoke();
    }

    public void ReorderShores(IReadOnlyList<string> orderedShoreIds)
    {
        lock (_sync)
        {
            var bucket = ActiveBucket();
            var known = new HashSet<string>(bucket.Shores.Keys, StringComparer.Ordinal);
            var next = new List<string>(bucket.Shores.Count);
            foreach (var id in orderedShoreIds)
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

    private Bucket? BucketFor(string bayId) => _workspace.Bays.TryGetValue(bayId, out var b) ? b : null;

    public string CreateWing(string bayId, string name)
    {
        var wingId = Guid.NewGuid().ToString("N");
        lock (_sync)
            EnsureBucket(bayId).Wings.Add(new WingState { Id = wingId, Name = name });
        OnBayChanged?.Invoke(bayId);
        return wingId;
    }

    public void RemoveWing(string bayId, string wingId)
    {
        if (wingId == MainWingId)
            return;
        lock (_sync)
        {
            if (BucketFor(bayId) is not { } bucket || bucket.Wings.RemoveAll(w => w.Id == wingId) == 0)
                return;
            foreach (var shore in bucket.Shores.Values)
                if (shore.WingId == wingId)
                    shore.WingId = MainWingId;
            if (bucket.ActiveWingId == wingId)
                bucket.ActiveWingId = MainWingId;
        }
        OnBayChanged?.Invoke(bayId);
    }

    public void RenameWing(string bayId, string wingId, string name)
    {
        lock (_sync)
        {
            if (BucketFor(bayId) is not { } bucket || bucket.Wings.FirstOrDefault(w => w.Id == wingId) is not { } wing)
                return;
            wing.Name = name;
        }
        OnBayChanged?.Invoke(bayId);
    }

    public void ReorderWings(string bayId, IReadOnlyList<string> orderedWingIds)
    {
        lock (_sync)
        {
            if (BucketFor(bayId) is not { } bucket)
                return;
            var known = bucket.Wings.ToDictionary(w => w.Id, StringComparer.Ordinal);
            var next = new List<WingState>(bucket.Wings.Count);
            foreach (var id in orderedWingIds)
                if (known.Remove(id, out var w))
                    next.Add(w);
            foreach (var w in bucket.Wings)
                if (known.ContainsKey(w.Id))
                    next.Add(w);
            bucket.Wings.Clear();
            bucket.Wings.AddRange(next);
        }
        OnBayChanged?.Invoke(bayId);
    }

    public void SetWingIcon(string bayId, string wingId, string? iconKind, string? iconValue)
    {
        lock (_sync)
        {
            if (BucketFor(bayId) is not { } bucket || bucket.Wings.FirstOrDefault(w => w.Id == wingId) is not { } wing)
                return;
            wing.IconKind = iconKind;
            wing.IconValue = iconValue;
        }
        OnBayChanged?.Invoke(bayId);
    }

    public void SwitchWing(string bayId, string wingId)
    {
        lock (_sync)
        {
            if (BucketFor(bayId) is not { } bucket || !bucket.Wings.Any(w => w.Id == wingId))
                return;
            bucket.ActiveWingId = wingId;
        }
        OnBayChanged?.Invoke(bayId);
    }

    public void SetShorePinned(string bayId, string shoreId, bool pinned)
    {
        lock (_sync)
        {
            if (BucketFor(bayId) is not { } bucket || !bucket.Shores.TryGetValue(shoreId, out var shore))
                return;
            shore.Pinned = pinned;
        }
        OnBayChanged?.Invoke(bayId);
    }

    public void MoveShoreToWing(string bayId, string shoreId, string wingId)
    {
        lock (_sync)
        {
            if (BucketFor(bayId) is not { } bucket || !bucket.Wings.Any(w => w.Id == wingId) || !bucket.Shores.TryGetValue(shoreId, out var shore))
                return;
            shore.WingId = wingId;
        }
        OnBayChanged?.Invoke(bayId);
    }

    public void SwitchShore(string bayId, string shoreId)
    {
        lock (_sync)
        {
            if (BucketFor(bayId) is not { } bucket || !bucket.Shores.ContainsKey(shoreId))
                return;
            bucket.ActiveShoreId = shoreId;
        }
        OnBayChanged?.Invoke(bayId);
    }

    public void RenameShore(string bayId, string shoreId, string name)
    {
        lock (_sync)
        {
            if (BucketFor(bayId) is not { } bucket || !bucket.Shores.TryGetValue(shoreId, out var shore))
                return;
            shore.Name = name;
        }
        OnBayChanged?.Invoke(bayId);
    }

    public IReadOnlyList<string> CloseShore(string bayId, string shoreId)
    {
        var nookIds = new List<string>();
        lock (_sync)
        {
            if (BucketFor(bayId) is not { } bucket || !bucket.Shores.TryGetValue(shoreId, out var rs))
                return nookIds;
            foreach (var leaf in MosaicOps.Leaves(rs.Root))
                if (!IsEmptyLeaf(leaf))
                    nookIds.Add(leaf.NookId);
            bucket.Shores.Remove(shoreId);
            bucket.Order.Remove(shoreId);
            _workspace.ShoreOwners.Remove(shoreId);
            if (bucket.ActiveShoreId == shoreId)
                bucket.ActiveShoreId = bucket.Order.Count > 0 ? bucket.Order[0] : null;
        }
        OnBayChanged?.Invoke(bayId);
        return nookIds;
    }

    public void SetFocusedNook(string bayId, string? nookId)
    {
        lock (_sync)
        {
            if (BucketFor(bayId) is not { } bucket)
                return;
            bucket.FocusedNookId = nookId;
        }
        OnBayChanged?.Invoke(bayId);
    }

    public string CreateShoreInWing(string bayId, string wingId, string name, NookLeaf firstLeaf)
    {
        string shoreId;
        lock (_sync)
        {
            var bucket = EnsureBucket(bayId);
            shoreId = Guid.NewGuid().ToString("N");
            bucket.Shores[shoreId] = new ShoreState
            {
                Name = name,
                Root = firstLeaf,
                ActiveNookId = firstLeaf.NookId,
                ZoomedNookId = null,
                WingId = bucket.Wings.Any(w => w.Id == wingId) ? wingId : MainWingId,
            };
            bucket.Order.Add(shoreId);
            bucket.ActiveShoreId = shoreId;
            _workspace.ShoreOwners[shoreId] = bayId;
        }
        OnBayChanged?.Invoke(bayId);
        return shoreId;
    }

    public IReadOnlyList<ShoreView> ShoresFor(string bayId)
    {
        lock (_sync)
        {
            if (BucketFor(bayId) is not { } bucket)
                return System.Array.Empty<ShoreView>();
            var list = new List<ShoreView>(bucket.Order.Count);
            foreach (var shoreId in bucket.Order)
                if (bucket.Shores.TryGetValue(shoreId, out var shore))
                    list.Add(new ShoreView(shoreId, shore.Name, shore.WingId, shore.Pinned, shoreId == bucket.ActiveShoreId));
            return list;
        }
    }

    public IReadOnlyList<WingView> WingsFor(string bayId)
    {
        lock (_sync)
        {
            if (BucketFor(bayId) is not { } bucket)
                return new[] { new WingView(MainWingId, "main", null, null) };
            return bucket.Wings.Select(w => new WingView(w.Id, w.Name, w.IconKind, w.IconValue)).ToList();
        }
    }

    public string? FocusedNookFor(string bayId)
    {
        lock (_sync)
            return BucketFor(bayId)?.FocusedNookId;
    }

    public string? ActiveShoreFor(string bayId)
    {
        lock (_sync)
            return BucketFor(bayId)?.ActiveShoreId;
    }

    public (string? BayId, string? ShoreId) ResolveNookLocation(string? nookId)
    {
        if (nookId is null)
            return (null, null);
        lock (_sync)
        {
            foreach (var (bayId, bucket) in _workspace.Bays)
                foreach (var (shoreId, shore) in bucket.Shores)
                    foreach (var leaf in MosaicOps.Leaves(shore.Root))
                        if (leaf.NookId == nookId)
                            return (bayId, shoreId);
        }
        return (null, null);
    }

    public bool MoveShoreToBay(string fromBayId, string shoreId, string toBayId)
    {
        lock (_sync)
        {
            if (BucketFor(fromBayId) is not { } from || BucketFor(toBayId) is not { } to)
                return false;
            if (!from.Shores.TryGetValue(shoreId, out var shore))
                return false;
            from.Shores.Remove(shoreId);
            from.Order.Remove(shoreId);
            if (from.ActiveShoreId == shoreId)
                from.ActiveShoreId = from.Order.Count > 0 ? from.Order[0] : null;
            shore.WingId = MainWingId;
            to.Shores[shoreId] = shore;
            to.Order.Add(shoreId);
            to.ActiveShoreId = shoreId;
            _workspace.ShoreOwners[shoreId] = toBayId;
        }
        OnBayChanged?.Invoke(fromBayId);
        OnBayChanged?.Invoke(toBayId);
        return true;
    }

    public BaySnapshot ToSnapshot(string id, string name, string projectDir)
    {
        lock (_sync)
        {
            var bucket = _workspace.Bays.TryGetValue(id, out var b)
                ? b
                : _workspace.Bays.Count == 1 && _workspace.Bays.TryGetValue(DefaultBayId, out var legacy)
                    ? legacy
                    : new Bucket();
            var shores = new List<ShoreSnapshot>(bucket.Shores.Count);
            foreach (var shoreId in bucket.Order)
            {
                if (!bucket.Shores.TryGetValue(shoreId, out var rs))
                    continue;
                shores.Add(new ShoreSnapshot
                {
                    Id = shoreId,
                    Name = rs.Name,
                    LayoutTree = rs.Root,
                    ZoomedNookId = rs.ZoomedNookId,
                    WingId = rs.WingId,
                    Pinned = rs.Pinned,
                });
            }
            var wings = bucket.Wings.Select(w => new WingSnapshot
            {
                Id = w.Id,
                Name = w.Name,
                IconKind = w.IconKind,
                IconValue = w.IconValue,
            }).ToList();

            return new BaySnapshot
            {
                Id = id,
                Name = name,
                ProjectDir = projectDir,
                ActiveShoreId = bucket.ActiveShoreId ?? (shores.Count > 0 ? shores[0].Id : null),
                Shores = shores,
                Wings = wings,
                ActiveWingId = bucket.ActiveWingId,
                FocusedNookId = bucket.FocusedNookId,
            };
        }
    }

    public void LoadSnapshot(BaySnapshot ws)
    {
        lock (_sync)
        {
            var bucket = new Bucket();
            bucket.Wings.Clear();
            foreach (var w in ws.Wings)
                bucket.Wings.Add(new WingState { Id = w.Id, Name = w.Name, IconKind = w.IconKind, IconValue = w.IconValue });
            if (!bucket.Wings.Any(w => w.Id == MainWingId))
                bucket.Wings.Insert(0, new WingState { Id = MainWingId, Name = "main" });
            foreach (var rs in ws.Shores)
            {
                var leaves = MosaicOps.Leaves(rs.LayoutTree);
                bucket.Shores[rs.Id] = new ShoreState
                {
                    Name = rs.Name,
                    Root = rs.LayoutTree,
                    ActiveNookId = leaves.Count > 0 ? leaves[0].NookId : null,
                    ZoomedNookId = rs.ZoomedNookId,
                    WingId = bucket.Wings.Any(w => w.Id == rs.WingId) ? rs.WingId : MainWingId,
                    Pinned = rs.Pinned,
                };
                bucket.Order.Add(rs.Id);
            }
            bucket.ActiveShoreId = ws.ActiveShoreId ?? (bucket.Order.Count > 0 ? bucket.Order[0] : null);
            bucket.ActiveWingId = ws.ActiveWingId is { } aw && bucket.Wings.Any(w => w.Id == aw) ? aw : MainWingId;
            bucket.FocusedNookId = ws.FocusedNookId;
            foreach (var shoreId in _workspace.ShoreOwners
                         .Where(pair => pair.Value == ws.Id)
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                _workspace.ShoreOwners.Remove(shoreId);
            }
            foreach (var shoreId in bucket.Order)
                _workspace.ShoreOwners[shoreId] = ws.Id;
            _workspace.Bays[ws.Id] = bucket;
            AppendOpenBay(ws.Id);
            _workspace.ActiveBayId = ws.Id;
        }
    }

    public MosaicNode? GetRoot(string shoreId)
    {
        lock (_sync)
        {
            if (!_workspace.ShoreOwners.TryGetValue(shoreId, out var wsId) || !_workspace.Bays.TryGetValue(wsId, out var bucket))
                return null;
            return bucket.Shores.TryGetValue(shoreId, out var shore) ? shore.Root : null;
        }
    }

    public string? GetActive(string shoreId)
    {
        lock (_sync)
        {
            if (!_workspace.ShoreOwners.TryGetValue(shoreId, out var wsId) || !_workspace.Bays.TryGetValue(wsId, out var bucket))
                return null;
            return bucket.Shores.TryGetValue(shoreId, out var shore) ? shore.ActiveNookId : null;
        }
    }

    public string? FocusedNookId()
    {
        lock (_sync)
        {
            var bucket = ActiveBucket();
            var shoreId = bucket.ActiveShoreId ?? (bucket.Order.Count > 0 ? bucket.Order[0] : null);
            if (shoreId is null || !bucket.Shores.TryGetValue(shoreId, out var shore))
                return null;
            return shore.ActiveNookId;
        }
    }

    public IReadOnlyList<string> LeafNookIds(string bayId)
    {
        var ids = new List<string>();
        lock (_sync)
        {
            if (!_workspace.Bays.TryGetValue(bayId, out var bucket))
                return ids;
            foreach (var shoreId in bucket.Order)
                if (bucket.Shores.TryGetValue(shoreId, out var rs))
                    foreach (var leaf in MosaicOps.Leaves(rs.Root))
                        if (!IsEmptyLeaf(leaf))
                            ids.Add(leaf.NookId);
        }
        return ids;
    }

    public static bool IsEmptyShore(MosaicNode node) => node is NookLeaf leaf && IsEmptyLeaf(leaf);

    private static bool IsEmptyLeaf(NookLeaf leaf)
        => leaf.Subtabs.Count == 0 || leaf.Subtabs.All(s => s.NookType == NookType.Empty);

    private static NookLeaf MakeEmptyLeaf()
    {
        var id = Guid.NewGuid().ToString("N");
        return new NookLeaf { NookId = id, Subtabs = new[] { new Subtab(id, NookType.Empty) } };
    }

    private Bucket ActiveBucket() => EnsureBucket(_workspace.ActiveBayId);

    private Bucket EnsureBucket(string bayId)
    {
        if (!_workspace.Bays.TryGetValue(bayId, out var bucket))
        {
            bucket = new Bucket();
            _workspace.Bays[bayId] = bucket;
        }
        return bucket;
    }

    private (Bucket Bucket, ShoreState Shore) GetShoreOrThrow(string shoreId)
    {
        if (!_workspace.ShoreOwners.TryGetValue(shoreId, out var wsId) || !_workspace.Bays.TryGetValue(wsId, out var bucket) || !bucket.Shores.TryGetValue(shoreId, out var shore))
            throw new KeyNotFoundException($"Unknown shore '{shoreId}'.");
        return (bucket, shore);
    }

    private void AppendOpenBay(string bayId)
    {
        if (_workspace.OpenBayIds.Count == 0
            && bayId != DefaultBayId
            && _workspace.Bays.TryGetValue(DefaultBayId, out var defaultBay)
            && defaultBay.Shores.Count == 0)
        {
            _workspace.Bays.Remove(DefaultBayId);
        }
        if (!_workspace.OpenBayIds.Contains(bayId, StringComparer.Ordinal))
            _workspace.OpenBayIds.Add(bayId);
    }
}
