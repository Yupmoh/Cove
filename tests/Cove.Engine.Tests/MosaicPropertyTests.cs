using Cove.Engine.Layout;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class MosaicPropertyTests
{
    private static NookLeaf Leaf(string id) => new()
    {
        NookId = id,
        Subtabs = new[] { new Subtab(id + "-d", NookType.Terminal) },
    };

    private static MosaicNode SplitChain(params string[] ids)
    {
        MosaicNode root = Leaf(ids[0]);
        for (int i = 1; i < ids.Length; i++)
            root = MosaicOps.Split(root, ids[i - 1], SplitOrientation.Row, Leaf(ids[i]));
        return root;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void Split_N_Times_Produces_N_Leaves_AllReachable(int n)
    {
        var ids = Enumerable.Range(0, n).Select(i => $"p{i}").ToArray();
        MosaicNode root = SplitChain(ids);

        var leaves = MosaicOps.Leaves(root);
        Assert.Equal(n, leaves.Count);
        Assert.Equal(ids.Length, leaves.Select(l => l.NookId).Distinct().Count());
        foreach (var id in ids)
            Assert.NotNull(MosaicOps.Find(root, id));
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(4, 0)]
    [InlineData(4, 1)]
    [InlineData(4, 2)]
    [InlineData(4, 3)]
    public void Close_AnyLeaf_ReducesCountByOne_AndRemovesIt(int n, int closeIdx)
    {
        var ids = Enumerable.Range(0, n).Select(i => $"p{i}").ToArray();
        MosaicNode root = SplitChain(ids);
        string target = ids[closeIdx];

        MosaicNode? after = MosaicOps.Close(root, target);

        Assert.NotNull(after);
        var leaves = MosaicOps.Leaves(after!);
        Assert.Equal(n - 1, leaves.Count);
        Assert.Null(MosaicOps.Find(after!, target));
        foreach (var id in ids.Where(id => id != target))
            Assert.NotNull(MosaicOps.Find(after!, id));
    }

    [Theory]
    [InlineData(3, 1)]
    [InlineData(3, -1)]
    [InlineData(5, 1)]
    [InlineData(5, -1)]
    public void FocusCycle_VisitsEveryLeaf_BeforeRepeating(int n, int dir)
    {
        var ids = Enumerable.Range(0, n).Select(i => $"p{i}").ToArray();
        MosaicNode root = SplitChain(ids);
        string start = ids[0];

        var visited = new HashSet<string> { start };
        string cur = start;
        for (int i = 0; i < n - 1; i++)
        {
            string? next = MosaicOps.NextNook(root, cur, dir);
            Assert.NotNull(next);
            cur = next!;
            Assert.DoesNotContain(cur, visited);
            visited.Add(cur);
        }
        Assert.Equal(n, visited.Count);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void ReplaceLeaf_PreservesStructure_AndOnlyTouchesTarget(int n)
    {
        var ids = Enumerable.Range(0, n).Select(i => $"p{i}").ToArray();
        MosaicNode root = SplitChain(ids);
        var before = MosaicOps.Leaves(root).Select(l => l.NookId).ToList();

        MosaicNode after = MosaicOps.ReplaceLeaf(root, ids[1], leaf => leaf with { NookId = "renamed" });

        var afterLeaves = MosaicOps.Leaves(after);
        Assert.Equal(n, afterLeaves.Count);
        Assert.Null(MosaicOps.Find(after, ids[1]));
        Assert.NotNull(MosaicOps.Find(after, "renamed"));
        foreach (var id in ids.Where(id => id != ids[1]))
            Assert.NotNull(MosaicOps.Find(after, id));
        var untouched = afterLeaves.Where(l => l.NookId != "renamed").Select(l => l.NookId).ToList();
        Assert.Equal(before.Where(id => id != ids[1]), untouched);
    }

    [Fact]
    public void Close_OnlyLeaf_ReturnsNull()
    {
        MosaicNode root = Leaf("solo");
        Assert.Null(MosaicOps.Close(root, "solo"));
    }
}
