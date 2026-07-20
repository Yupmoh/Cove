using Cove.Engine.Layout;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class MosaicOpsTests
{
    [Fact]
    public void BalanceStack_FourNestedLanesBecomeBalancedPairs()
    {
        var root = Split(
            SplitOrientation.Column,
            Leaf("a"),
            Split(
                SplitOrientation.Column,
                Leaf("b"),
                Split(
                    SplitOrientation.Column,
                    Leaf("c"),
                    Leaf("d"))));

        var result = MosaicOps.BalanceStack(
            root,
            "d",
            SplitOrientation.Column);

        Assert.NotNull(result);
        Assert.Equal(4, result.Value.Nooks);
        var balanced = Assert.IsType<SplitNode>(result.Value.Root);
        Assert.Equal(0.5, balanced.Ratio);
        var left = Assert.IsType<SplitNode>(balanced.ChildA);
        var right = Assert.IsType<SplitNode>(balanced.ChildB);
        Assert.Equal(0.5, left.Ratio);
        Assert.Equal(0.5, right.Ratio);
        Assert.Equal(
            ["a", "b", "c", "d"],
            MosaicOps.Leaves(balanced).Select(leaf => leaf.NookId));
    }

    [Fact]
    public void BalanceStack_ThreeLanesUseEqualShareRatios()
    {
        var root = Split(
            SplitOrientation.Column,
            Leaf("a"),
            Split(
                SplitOrientation.Column,
                Leaf("b"),
                Leaf("c")));

        var result = MosaicOps.BalanceStack(
            root,
            "c",
            SplitOrientation.Column);

        Assert.NotNull(result);
        var balanced = Assert.IsType<SplitNode>(result.Value.Root);
        Assert.Equal(1.0 / 3.0, balanced.Ratio, 10);
        Assert.Equal(
            0.5,
            Assert.IsType<SplitNode>(balanced.ChildB).Ratio);
        Assert.Equal(3, result.Value.Nooks);
    }

    [Fact]
    public void BalanceStack_PreservesOrthogonalSubtreeAsAtomicLane()
    {
        var orthogonal = Split(
            SplitOrientation.Row,
            Leaf("b"),
            Leaf("x"));
        var column = Split(
            SplitOrientation.Column,
            Leaf("a"),
            Split(
                SplitOrientation.Column,
                orthogonal,
                Leaf("c")));
        var outside = Leaf("outside");
        var root = Split(
            SplitOrientation.Row,
            outside,
            column,
            0.25);

        var result = MosaicOps.BalanceStack(
            root,
            "c",
            SplitOrientation.Column);

        Assert.NotNull(result);
        var outer = Assert.IsType<SplitNode>(result.Value.Root);
        Assert.Equal(SplitOrientation.Row, outer.Orientation);
        Assert.Equal(0.25, outer.Ratio);
        Assert.Same(outside, outer.ChildA);
        var balanced = Assert.IsType<SplitNode>(outer.ChildB);
        Assert.Equal(1.0 / 3.0, balanced.Ratio, 10);
        Assert.Same(
            orthogonal,
            Assert.IsType<SplitNode>(balanced.ChildB).ChildA);
        Assert.Equal(3, result.Value.Nooks);
    }

    [Fact]
    public void BalanceStack_MissingTargetOrAxisReturnsNull()
    {
        var root = Split(
            SplitOrientation.Row,
            Leaf("a"),
            Leaf("b"));

        Assert.Null(MosaicOps.BalanceStack(
            root,
            "missing",
            SplitOrientation.Row));
        Assert.Null(MosaicOps.BalanceStack(
            root,
            "a",
            SplitOrientation.Column));
    }

    private static SplitNode Split(
        SplitOrientation orientation,
        MosaicNode childA,
        MosaicNode childB,
        double ratio = 0.5) => new()
        {
            Orientation = orientation,
            Ratio = ratio,
            ChildA = childA,
            ChildB = childB,
        };

    private static NookLeaf Leaf(string nookId) => new()
    {
        NookId = nookId,
        Subtabs = [new Subtab(nookId, NookType.Terminal)],
    };
}
