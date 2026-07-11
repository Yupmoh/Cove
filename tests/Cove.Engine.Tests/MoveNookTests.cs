using Cove.Engine.Layout;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class MoveNookTests
{
    private static NookLeaf Leaf(string id, params string[] extraSubtabs)
    {
        var subs = new Subtab[1 + extraSubtabs.Length];
        subs[0] = new Subtab(id, NookType.Terminal);
        for (var i = 0; i < extraSubtabs.Length; i++)
            subs[i + 1] = new Subtab(extraSubtabs[i], NookType.Terminal);
        return new NookLeaf { NookId = id, Subtabs = subs };
    }

    private static (LayoutService Layout, string ShoreId) ShoreWithThree()
    {
        var layout = new LayoutService();
        var shoreId = layout.CreateShore("shore", Leaf("a"));
        layout.SplitNook(shoreId, "a", SplitOrientation.Row, Leaf("b"));
        layout.SplitNook(shoreId, "b", SplitOrientation.Row, Leaf("c"));
        return (layout, shoreId);
    }

    private static string[] LeafOrder(LayoutService layout, string shoreId)
    {
        var shore = layout.ToSnapshot("w", "w", "/tmp").Shores.Single(r => r.Id == shoreId);
        return MosaicOps.Leaves(shore.LayoutTree).Select(l => l.NookId).ToArray();
    }

    [Fact]
    public void MoveNook_AfterTarget_ReordersLeaves()
    {
        var (layout, shoreId) = ShoreWithThree();
        layout.MoveNook(shoreId, "a", "c", SplitOrientation.Row, 1);
        Assert.Equal(new[] { "b", "c", "a" }, LeafOrder(layout, shoreId));
    }

    [Fact]
    public void MoveNook_BeforeTarget_InsertsOnNearSide()
    {
        var (layout, shoreId) = ShoreWithThree();
        layout.MoveNook(shoreId, "c", "a", SplitOrientation.Row, -1);
        Assert.Equal(new[] { "c", "a", "b" }, LeafOrder(layout, shoreId));
    }

    [Fact]
    public void MoveNook_PreservesSubtabs()
    {
        var layout = new LayoutService();
        var shoreId = layout.CreateShore("shore", Leaf("a", "a2"));
        layout.SplitNook(shoreId, "a", SplitOrientation.Row, Leaf("b"));
        layout.MoveNook(shoreId, "a", "b", SplitOrientation.Column, 1);
        var shore = layout.ToSnapshot("w", "w", "/tmp").Shores.Single(r => r.Id == shoreId);
        var moved = MosaicOps.Leaves(shore.LayoutTree).Single(l => l.NookId == "a");
        Assert.Equal(2, moved.Subtabs.Count);
    }

    [Fact]
    public void MoveNook_OntoItself_Throws()
    {
        var (layout, shoreId) = ShoreWithThree();
        Assert.Throws<InvalidOperationException>(() => layout.MoveNook(shoreId, "a", "a", SplitOrientation.Row, 1));
    }

    [Fact]
    public void MoveNook_OnlyNook_Throws()
    {
        var layout = new LayoutService();
        var shoreId = layout.CreateShore("shore", Leaf("a"));
        Assert.Throws<InvalidOperationException>(() => layout.MoveNook(shoreId, "a", "a", SplitOrientation.Row, 1));
    }

    [Fact]
    public void CloseNook_AfterMove_CollapsesSplitsWithoutGhostLeaves()
    {
        var (layout, shoreId) = ShoreWithThree();
        layout.MoveNook(shoreId, "a", "c", SplitOrientation.Column, 1);
        layout.CloseNook(shoreId, "c");
        layout.CloseNook(shoreId, "a");

        var shore = layout.ToSnapshot("w", "w", "/tmp").Shores.Single(r => r.Id == shoreId);
        var leaf = Assert.IsType<NookLeaf>(shore.LayoutTree);
        Assert.Equal("b", leaf.NookId);
    }
}
