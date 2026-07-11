using System.Linq;
using Cove.Engine.Layout;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class LayoutBaysTests
{
    private static NookLeaf Leaf(string id, NookType type = NookType.Terminal) => new()
    {
        NookId = id,
        Subtabs = new[] { new Subtab(id, type) },
    };

    [Fact]
    public void SwitchBay_SwapsRenderedShoreSet()
    {
        var svc = new LayoutService();
        svc.SetActiveBay("wsA");
        var a = svc.CreateShore("A-shore", Leaf("a"));
        svc.SetActiveBay("wsB");
        var b = svc.CreateShore("B-shore", Leaf("b"));

        var snapB = svc.ToSnapshot("wsB", "B", "/b");
        Assert.Single(snapB.Shores);
        Assert.Equal(b, snapB.Shores[0].Id);

        svc.SetActiveBay("wsA");
        var snapA = svc.ToSnapshot("wsA", "A", "/a");
        Assert.Single(snapA.Shores);
        Assert.Equal(a, snapA.Shores[0].Id);
    }

    [Fact]
    public void ActiveBayId_TracksSwitch()
    {
        var svc = new LayoutService();
        Assert.Equal(LayoutService.DefaultBayId, svc.ActiveBayId);
        svc.SetActiveBay("wsX");
        Assert.Equal("wsX", svc.ActiveBayId);
    }

    [Fact]
    public void RemoveBay_ReturnsNookIds_AndDropsShores()
    {
        var svc = new LayoutService();
        svc.SetActiveBay("wsA");
        var shore = svc.CreateShore("A", Leaf("a"));
        svc.SplitNook(shore, "a", SplitOrientation.Row, Leaf("b"));

        var nookIds = svc.RemoveBay("wsA");
        Assert.Contains("a", nookIds);
        Assert.Contains("b", nookIds);
        Assert.Empty(svc.ToSnapshot("wsA", "A", "/a").Shores);
    }

    [Fact]
    public void EmptyShore_IsLegal_AndSnapshots()
    {
        var svc = new LayoutService();
        var shore = svc.CreateShore("Terminal 1", Leaf("empty-1", NookType.Empty));
        Assert.True(LayoutService.IsEmptyShore(svc.GetRoot(shore)!));
    }

    [Fact]
    public void ReplaceNook_PutsNookIntoEmptyShore()
    {
        var svc = new LayoutService();
        var shore = svc.CreateShore("Terminal 1", Leaf("empty-1", NookType.Empty));
        svc.ReplaceNook(shore, "empty-1", Leaf("real-1"));
        Assert.False(LayoutService.IsEmptyShore(svc.GetRoot(shore)!));
        Assert.Equal("real-1", svc.GetActive(shore));
        var leaves = MosaicOps.Leaves(svc.GetRoot(shore)!);
        Assert.Single(leaves);
        Assert.Equal("real-1", leaves[0].NookId);
    }

    [Fact]
    public void ClosingLastNook_ConvertsShoreToEmpty_NotRemoved()
    {
        var svc = new LayoutService();
        var shore = svc.CreateShore("Terminal 1", Leaf("a"));
        svc.CloseNook(shore, "a");
        var root = svc.GetRoot(shore);
        Assert.NotNull(root);
        Assert.True(LayoutService.IsEmptyShore(root!));
    }

    [Fact]
    public void CloseShore_RemovesShoreEntirely()
    {
        var svc = new LayoutService();
        var shore = svc.CreateShore("Terminal 1", Leaf("a"));
        var nookIds = svc.CloseShore(shore);
        Assert.Contains("a", nookIds);
        Assert.Null(svc.GetRoot(shore));
        Assert.Empty(svc.ToSnapshot(LayoutService.DefaultBayId, "d", "/d").Shores);
    }

    [Fact]
    public void LoadSnapshot_KeepsOtherBays()
    {
        var svc = new LayoutService();
        svc.SetActiveBay("wsA");
        svc.CreateShore("A", Leaf("a"));

        var other = new BaySnapshot
        {
            Id = "wsB",
            Name = "B",
            ProjectDir = "/b",
            Shores = new[] { new ShoreSnapshot { Id = "rB", Name = "B-shore", LayoutTree = Leaf("b") } },
        };
        svc.LoadSnapshot(other);

        Assert.Single(svc.ToSnapshot("wsA", "A", "/a").Shores);
        Assert.Single(svc.ToSnapshot("wsB", "B", "/b").Shores);
    }
}
