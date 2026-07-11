using Cove.Engine.Layout;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SubtabTests
{
    private static NookLeaf Leaf(string id) => new()
    {
        NookId = id,
        Subtabs = new[] { new Subtab(id, NookType.Terminal) },
    };

    [Fact]
    public void ReplaceLeaf_TransformsTarget()
    {
        MosaicNode root = new SplitNode
        {
            Orientation = SplitOrientation.Row,
            Ratio = 0.5,
            ChildA = Leaf("a"),
            ChildB = Leaf("b"),
        };

        var result = MosaicOps.ReplaceLeaf(root, "b", l => l with { ActiveSubtab = 5 });

        var b = MosaicOps.Find(result, "b");
        Assert.NotNull(b);
        Assert.Equal(5, b!.ActiveSubtab);

        var a = MosaicOps.Find(result, "a");
        Assert.NotNull(a);
        Assert.Equal(0, a!.ActiveSubtab);
        Assert.Single(a.Subtabs);
        Assert.Equal("a", a.Subtabs[0].DocumentId);
    }

    [Fact]
    public void AddSubtab_AppendsAndActivates()
    {
        var s = new LayoutService();
        var rid = s.CreateShore("m", Leaf("p1"));

        s.AddSubtab(rid, "p1", "p2");

        var leaf = MosaicOps.Find(s.GetRoot(rid)!, "p1");
        Assert.NotNull(leaf);
        Assert.Equal(2, leaf!.Subtabs.Count);
        Assert.Equal("p2", leaf.Subtabs[1].DocumentId);
        Assert.Equal(1, leaf.ActiveSubtab);
    }

    [Fact]
    public void ActivateSubtab_SetsIndexClamped()
    {
        var s = new LayoutService();
        var rid = s.CreateShore("m", Leaf("p1"));
        s.AddSubtab(rid, "p1", "p2");

        s.ActivateSubtab(rid, "p1", 0);
        var leaf = MosaicOps.Find(s.GetRoot(rid)!, "p1");
        Assert.NotNull(leaf);
        Assert.Equal(0, leaf!.ActiveSubtab);

        s.ActivateSubtab(rid, "p1", 99);
        leaf = MosaicOps.Find(s.GetRoot(rid)!, "p1");
        Assert.NotNull(leaf);
        Assert.Equal(1, leaf!.ActiveSubtab);
    }
}
