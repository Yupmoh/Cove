using Cove.Engine.Layout;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class MoveNookToShoreTests
{
    private static NookLeaf Leaf(string id) => new NookLeaf { NookId = id, Subtabs = new[] { new Subtab(id, NookType.Terminal) } };

    private static NookLeaf EmptyLeaf(string id) => new NookLeaf { NookId = id, Subtabs = new[] { new Subtab(id, NookType.Empty) } };

    [Fact]
    public void MoveNookToShore_AppendsToPopulatedTarget()
    {
        var layout = new LayoutService();
        var r1 = layout.CreateShore("one", Leaf("a"));
        layout.SplitNook(r1, "a", SplitOrientation.Row, Leaf("b"));
        var r2 = layout.CreateShore("two", Leaf("c"));

        layout.MoveNookToShore("a", r2);

        var snap = layout.ToSnapshot("w", "w", "/tmp");
        var shore1 = snap.Shores.Single(r => r.Id == r1);
        var shore2 = snap.Shores.Single(r => r.Id == r2);
        Assert.Equal(new[] { "b" }, MosaicOps.Leaves(shore1.LayoutTree).Select(l => l.NookId));
        Assert.Equal(new[] { "c", "a" }, MosaicOps.Leaves(shore2.LayoutTree).Select(l => l.NookId));
    }

    [Fact]
    public void MoveNookToShore_ReplacesEmptyTargetShore()
    {
        var layout = new LayoutService();
        var r1 = layout.CreateShore("one", Leaf("a"));
        layout.SplitNook(r1, "a", SplitOrientation.Row, Leaf("b"));
        var r2 = layout.CreateShore("two", EmptyLeaf("empty-1"));

        layout.MoveNookToShore("a", r2);

        var snap = layout.ToSnapshot("w", "w", "/tmp");
        var shore2 = snap.Shores.Single(r => r.Id == r2);
        Assert.Equal(new[] { "a" }, MosaicOps.Leaves(shore2.LayoutTree).Select(l => l.NookId));
    }

    [Fact]
    public void MoveNookToShore_LastNookLeavesSourceShoreEmpty()
    {
        var layout = new LayoutService();
        var r1 = layout.CreateShore("one", Leaf("a"));
        var r2 = layout.CreateShore("two", Leaf("b"));

        layout.MoveNookToShore("a", r2);

        var snap = layout.ToSnapshot("w", "w", "/tmp");
        var shore1 = snap.Shores.Single(r => r.Id == r1);
        var leaves = MosaicOps.Leaves(shore1.LayoutTree);
        Assert.Single(leaves);
        Assert.All(leaves[0].Subtabs, s => Assert.Equal(NookType.Empty, s.NookType));
    }

    [Fact]
    public void MoveNookToShore_SameShore_Throws()
    {
        var layout = new LayoutService();
        var r1 = layout.CreateShore("one", Leaf("a"));
        Assert.Throws<InvalidOperationException>(() => layout.MoveNookToShore("a", r1));
    }

    [Fact]
    public void MoveNookToShore_UnknownNook_Throws()
    {
        var layout = new LayoutService();
        var r1 = layout.CreateShore("one", Leaf("a"));
        Assert.Throws<KeyNotFoundException>(() => layout.MoveNookToShore("nope", r1));
    }
}
