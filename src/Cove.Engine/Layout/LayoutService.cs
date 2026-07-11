using System;
using System.Collections.Generic;
using System.Linq;
using Cove.Persistence;

namespace Cove.Engine.Layout;

public sealed class LayoutService
{
    public const string DefaultBayId = "default";

    private sealed class ShoreState
    {
        public required string Name { get; set; }
        public required MosaicNode Root { get; set; }
        public string? ActiveNookId { get; set; }
        public string? ZoomedNookId { get; set; }
    }

    private sealed class Bucket
    {
        public readonly Dictionary<string, ShoreState> Shores = new(StringComparer.Ordinal);
        public readonly List<string> Order = new();
        public string? ActiveShoreId;
    }

    private readonly Dictionary<string, Bucket> _buckets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _shoreToBay = new(StringComparer.Ordinal);
    private string _activeBayId = DefaultBayId;
    private readonly object _sync = new();
    public Action? OnChanged { get; set; }

    public LayoutService()
    {
        _buckets[DefaultBayId] = new Bucket();
    }

    public string ActiveBayId
    {
        get { lock (_sync) return _activeBayId; }
    }

    public IReadOnlyList<string> BayIds
    {
        get { lock (_sync) return _buckets.Keys.ToList(); }
    }

    public void SetActiveBay(string bayId)
    {
        lock (_sync)
        {
            EnsureBucket(bayId);
            _activeBayId = bayId;
        }
        OnChanged?.Invoke();
    }

    public void EnsureBay(string bayId)
    {
        lock (_sync)
            EnsureBucket(bayId);
    }

    public IReadOnlyList<string> RemoveBay(string bayId)
    {
        var nookIds = new List<string>();
        lock (_sync)
        {
            if (!_buckets.TryGetValue(bayId, out var bucket))
                return nookIds;
            foreach (var shoreId in bucket.Order)
            {
                _shoreToBay.Remove(shoreId);
                if (bucket.Shores.TryGetValue(shoreId, out var rs))
                    foreach (var leaf in MosaicOps.Leaves(rs.Root))
                        if (!IsEmptyLeaf(leaf))
                            nookIds.Add(leaf.NookId);
            }
            _buckets.Remove(bayId);
            if (_activeBayId == bayId)
                _activeBayId = _buckets.Keys.FirstOrDefault() ?? DefaultBayId;
            EnsureBucket(_activeBayId);
        }
        OnChanged?.Invoke();
        return nookIds;
    }

    public string CreateShore(string name, NookLeaf firstLeaf)
    {
        string shoreId;
        lock (_sync)
        {
            var bucket = ActiveBucket();
            shoreId = Guid.NewGuid().ToString("N");
            bucket.Shores[shoreId] = new ShoreState
            {
                Name = name,
                Root = firstLeaf,
                ActiveNookId = firstLeaf.NookId,
                ZoomedNookId = null,
            };
            bucket.Order.Add(shoreId);
            bucket.ActiveShoreId = shoreId;
            _shoreToBay[shoreId] = _activeBayId;
        }
        OnChanged?.Invoke();
        return shoreId;
    }

    public void SplitNook(string shoreId, string targetNookId, SplitOrientation orient, NookLeaf newLeaf)
    {
        lock (_sync)
        {
            var (bucket, shore) = GetShoreOrThrow(shoreId);
            shore.Root = MosaicOps.Split(shore.Root, targetNookId, orient, newLeaf);
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
            foreach (var bucket in _buckets.Values)
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
            var (_, shore) = GetShoreOrThrow(shoreId);
            var next = MosaicOps.Close(shore.Root, nookId);
            if (next is null)
            {
                shore.Root = MakeEmptyLeaf();
                shore.ActiveNookId = ((NookLeaf)shore.Root).NookId;
                shore.ZoomedNookId = null;
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
        }
        OnChanged?.Invoke();
    }

    public IReadOnlyList<string> CloseShore(string shoreId)
    {
        var nookIds = new List<string>();
        lock (_sync)
        {
            if (!_shoreToBay.TryGetValue(shoreId, out var wsId) || !_buckets.TryGetValue(wsId, out var bucket))
                return nookIds;
            if (bucket.Shores.TryGetValue(shoreId, out var rs))
                foreach (var leaf in MosaicOps.Leaves(rs.Root))
                    if (!IsEmptyLeaf(leaf))
                        nookIds.Add(leaf.NookId);
            bucket.Shores.Remove(shoreId);
            bucket.Order.Remove(shoreId);
            _shoreToBay.Remove(shoreId);
            if (bucket.ActiveShoreId == shoreId)
                bucket.ActiveShoreId = bucket.Order.Count > 0 ? bucket.Order[0] : null;
        }
        OnChanged?.Invoke();
        return nookIds;
    }

    public void FocusNook(string shoreId, string nookId)
    {
        lock (_sync)
        {
            var (bucket, shore) = GetShoreOrThrow(shoreId);
            shore.ActiveNookId = nookId;
            bucket.ActiveShoreId = shoreId;
        }
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
        lock (_sync)
        {
            var (_, shore) = GetShoreOrThrow(shoreId);
            shore.Name = name;
        }
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

    public BaySnapshot ToSnapshot(string id, string name, string projectDir)
    {
        lock (_sync)
        {
            var bucket = _buckets.TryGetValue(id, out var b) ? b : ActiveBucket();
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
                });
            }

            return new BaySnapshot
            {
                Id = id,
                Name = name,
                ProjectDir = projectDir,
                ActiveShoreId = bucket.ActiveShoreId ?? (shores.Count > 0 ? shores[0].Id : null),
                Shores = shores,
            };
        }
    }

    public void LoadSnapshot(BaySnapshot ws)
    {
        lock (_sync)
        {
            var bucket = new Bucket();
            foreach (var rs in ws.Shores)
            {
                var leaves = MosaicOps.Leaves(rs.LayoutTree);
                bucket.Shores[rs.Id] = new ShoreState
                {
                    Name = rs.Name,
                    Root = rs.LayoutTree,
                    ActiveNookId = leaves.Count > 0 ? leaves[0].NookId : null,
                    ZoomedNookId = rs.ZoomedNookId,
                };
                bucket.Order.Add(rs.Id);
                _shoreToBay[rs.Id] = ws.Id;
            }
            bucket.ActiveShoreId = ws.ActiveShoreId ?? (bucket.Order.Count > 0 ? bucket.Order[0] : null);
            _buckets[ws.Id] = bucket;
            _activeBayId = ws.Id;
        }
    }

    public MosaicNode? GetRoot(string shoreId)
    {
        lock (_sync)
        {
            if (!_shoreToBay.TryGetValue(shoreId, out var wsId) || !_buckets.TryGetValue(wsId, out var bucket))
                return null;
            return bucket.Shores.TryGetValue(shoreId, out var shore) ? shore.Root : null;
        }
    }

    public string? GetActive(string shoreId)
    {
        lock (_sync)
        {
            if (!_shoreToBay.TryGetValue(shoreId, out var wsId) || !_buckets.TryGetValue(wsId, out var bucket))
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
            if (!_buckets.TryGetValue(bayId, out var bucket))
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

    private Bucket ActiveBucket() => EnsureBucket(_activeBayId);

    private Bucket EnsureBucket(string bayId)
    {
        if (!_buckets.TryGetValue(bayId, out var bucket))
        {
            bucket = new Bucket();
            _buckets[bayId] = bucket;
        }
        return bucket;
    }

    private (Bucket Bucket, ShoreState Shore) GetShoreOrThrow(string shoreId)
    {
        if (!_shoreToBay.TryGetValue(shoreId, out var wsId) || !_buckets.TryGetValue(wsId, out var bucket) || !bucket.Shores.TryGetValue(shoreId, out var shore))
            throw new KeyNotFoundException($"Unknown shore '{shoreId}'.");
        return (bucket, shore);
    }
}
