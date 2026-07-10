using System;
using System.Collections.Generic;
using Cove.Persistence;

namespace Cove.Engine.Layout;

public static class MosaicOps
{
    public static IReadOnlyList<PaneLeaf> Leaves(MosaicNode root)
    {
        var list = new List<PaneLeaf>();
        Collect(root, list);
        return list;
    }

    private static void Collect(MosaicNode node, List<PaneLeaf> list)
    {
        switch (node)
        {
            case PaneLeaf leaf:
                list.Add(leaf);
                break;
            case SplitNode split:
                Collect(split.ChildA, list);
                Collect(split.ChildB, list);
                break;
        }
    }

    public static PaneLeaf? Find(MosaicNode root, string paneId)
    {
        return root switch
        {
            PaneLeaf leaf => leaf.PaneId == paneId ? leaf : null,
            SplitNode split => Find(split.ChildA, paneId) ?? Find(split.ChildB, paneId),
            _ => null,
        };
    }

    public static MosaicNode ReplaceLeaf(MosaicNode root, string paneId, Func<PaneLeaf, PaneLeaf> transform)
    {
        return Replace(root);

        MosaicNode Replace(MosaicNode node)
        {
            if (node is PaneLeaf leaf)
            {
                if (leaf.PaneId == paneId)
                    return transform(leaf);
                return leaf;
            }

            if (node is SplitNode split)
            {
                var newA = Replace(split.ChildA);
                var newB = Replace(split.ChildB);
                if (ReferenceEquals(newA, split.ChildA) && ReferenceEquals(newB, split.ChildB))
                    return split;
                return split with { ChildA = newA, ChildB = newB };
            }

            return node;
        }
    }

    public static MosaicNode Split(MosaicNode root, string targetPaneId, SplitOrientation orient, PaneLeaf newLeaf, double ratio = 0.5, bool before = false)
    {
        var found = false;
        var result = Replace(root);
        if (!found)
            throw new InvalidOperationException($"Target pane '{targetPaneId}' not found.");
        return result;

        MosaicNode Replace(MosaicNode node)
        {
            if (node is PaneLeaf leaf)
            {
                if (leaf.PaneId == targetPaneId)
                {
                    found = true;
                    return new SplitNode
                    {
                        Orientation = orient,
                        Ratio = ratio,
                        ChildA = before ? newLeaf : leaf,
                        ChildB = before ? leaf : newLeaf,
                    };
                }
                return leaf;
            }

            if (node is SplitNode split)
            {
                var newA = Replace(split.ChildA);
                var newB = Replace(split.ChildB);
                if (ReferenceEquals(newA, split.ChildA) && ReferenceEquals(newB, split.ChildB))
                    return split;
                return split with { ChildA = newA, ChildB = newB };
            }

            return node;
        }
    }

    public static MosaicNode? Close(MosaicNode root, string paneId)
    {
        return CloseNode(root, paneId);
    }

    private static MosaicNode? CloseNode(MosaicNode node, string paneId)
    {
        if (node is PaneLeaf leaf)
        {
            return leaf.PaneId == paneId ? null : node;
        }

        if (node is SplitNode split)
        {
            if (split.ChildA is PaneLeaf a && a.PaneId == paneId)
                return split.ChildB;
            if (split.ChildB is PaneLeaf b && b.PaneId == paneId)
                return split.ChildA;

            var newA = CloseNode(split.ChildA, paneId);
            var newB = CloseNode(split.ChildB, paneId);

            if (newA is null)
                return split.ChildB;
            if (newB is null)
                return split.ChildA;

            if (ReferenceEquals(newA, split.ChildA) && ReferenceEquals(newB, split.ChildB))
                return split;
            return split with { ChildA = newA, ChildB = newB };
        }

        return node;
    }

    public static string? NextPane(MosaicNode root, string activePaneId, int dir)
    {
        var leaves = Leaves(root);
        if (leaves.Count < 2)
            return null;

        var index = -1;
        for (var i = 0; i < leaves.Count; i++)
        {
            if (leaves[i].PaneId == activePaneId)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
            return null;

        var count = leaves.Count;
        var next = ((index + dir) % count + count) % count;
        return leaves[next].PaneId;
    }
}
