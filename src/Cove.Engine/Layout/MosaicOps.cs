using System;
using System.Collections.Generic;
using Cove.Persistence;

namespace Cove.Engine.Layout;

public static class MosaicOps
{
    public static IReadOnlyList<NookLeaf> Leaves(MosaicNode root)
    {
        var list = new List<NookLeaf>();
        Collect(root, list);
        return list;
    }

    private static void Collect(MosaicNode node, List<NookLeaf> list)
    {
        switch (node)
        {
            case NookLeaf leaf:
                list.Add(leaf);
                break;
            case SplitNode split:
                Collect(split.ChildA, list);
                Collect(split.ChildB, list);
                break;
        }
    }

    public static NookLeaf? Find(MosaicNode root, string nookId)
    {
        return root switch
        {
            NookLeaf leaf => leaf.NookId == nookId ? leaf : null,
            SplitNode split => Find(split.ChildA, nookId) ?? Find(split.ChildB, nookId),
            _ => null,
        };
    }

    public static MosaicNode ReplaceLeaf(MosaicNode root, string nookId, Func<NookLeaf, NookLeaf> transform)
    {
        return Replace(root);

        MosaicNode Replace(MosaicNode node)
        {
            if (node is NookLeaf leaf)
            {
                if (leaf.NookId == nookId)
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

    public static MosaicNode Split(MosaicNode root, string targetNookId, SplitOrientation orient, NookLeaf newLeaf, double ratio = 0.5, bool before = false)
    {
        var found = false;
        var result = Replace(root);
        if (!found)
            throw new InvalidOperationException($"Target nook '{targetNookId}' not found.");
        return result;

        MosaicNode Replace(MosaicNode node)
        {
            if (node is NookLeaf leaf)
            {
                if (leaf.NookId == targetNookId)
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

    public static MosaicNode? Close(MosaicNode root, string nookId)
    {
        return CloseNode(root, nookId);
    }

    private static MosaicNode? CloseNode(MosaicNode node, string nookId)
    {
        if (node is NookLeaf leaf)
        {
            return leaf.NookId == nookId ? null : node;
        }

        if (node is SplitNode split)
        {
            if (split.ChildA is NookLeaf a && a.NookId == nookId)
                return split.ChildB;
            if (split.ChildB is NookLeaf b && b.NookId == nookId)
                return split.ChildA;

            var newA = CloseNode(split.ChildA, nookId);
            var newB = CloseNode(split.ChildB, nookId);

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

    public static string? NextNook(MosaicNode root, string activeNookId, int dir)
    {
        var leaves = Leaves(root);
        if (leaves.Count < 2)
            return null;

        var index = -1;
        for (var i = 0; i < leaves.Count; i++)
        {
            if (leaves[i].NookId == activeNookId)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
            return null;

        var count = leaves.Count;
        var next = ((index + dir) % count + count) % count;
        return leaves[next].NookId;
    }
}
