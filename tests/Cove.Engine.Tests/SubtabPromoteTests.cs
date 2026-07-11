using Cove.Engine.Layout;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SubtabPromoteTests
{
    private static NookLeaf Leaf(string id, params string[] docIds) => new()
    {
        NookId = id,
        Subtabs = docIds.Length == 0 ? new[] { new Subtab(id, NookType.Terminal) } : System.Array.ConvertAll(docIds, d => new Subtab(d, NookType.Terminal)),
    };

    [Fact]
    public void Promote_RemovesSubtabFromSource_SplitsIntoNewNook()
    {
        var layout = new LayoutService();
        string shoreId = layout.CreateShore("main", Leaf("p1", "p1", "p2", "p3"));
        layout.PromoteSubtab(shoreId, "p1", 1, "newNook");
        var root = layout.GetRoot(shoreId)!;
        Assert.IsType<SplitNode>(root);
        var split = (SplitNode)root;
        var source = split.ChildA is NookLeaf la && la.NookId == "p1" ? la : (split.ChildB as NookLeaf)!;
        var promoted = split.ChildA is NookLeaf lb && lb.NookId == "newNook" ? lb : (split.ChildB as NookLeaf)!;
        Assert.Equal(2, source.Subtabs.Count);
        Assert.DoesNotContain(source.Subtabs, s => s.DocumentId == "p2");
        Assert.Single(promoted.Subtabs);
        Assert.Equal("p2", promoted.Subtabs[0].DocumentId);
    }

    [Fact]
    public void Promote_SingleSubtab_Fails()
    {
        var layout = new LayoutService();
        string shoreId = layout.CreateShore("main", Leaf("p1", "p1"));
        Assert.Throws<System.InvalidOperationException>(() => layout.PromoteSubtab(shoreId, "p1", 0, "newNook"));
    }

    [Fact]
    public void CenterDrop_MovesSubtabBetweenLeaves()
    {
        var layout = new LayoutService();
        string shoreId = layout.CreateShore("main", Leaf("p1", "p1", "p2", "p3"));
        layout.SplitNook(shoreId, "p1", SplitOrientation.Row, Leaf("p2", "p2"));
        var beforeSplit = (SplitNode)layout.GetRoot(shoreId)!;
        var targetBefore = beforeSplit.ChildA is NookLeaf la && la.NookId == "p2" ? la
            : beforeSplit.ChildB is NookLeaf lb && lb.NookId == "p2" ? lb
            : throw new System.Exception("p2 leaf not found");
        Assert.Single(targetBefore.Subtabs);

        layout.CenterDrop(shoreId, "p1", 1, "p2");

        var root = layout.GetRoot(shoreId)!;
        var split = (SplitNode)root;
        var source = split.ChildA is NookLeaf sa && sa.NookId == "p1" ? sa : (split.ChildB as NookLeaf)!;
        var target = split.ChildA is NookLeaf sb && sb.NookId == "p2" ? sb : (split.ChildB as NookLeaf)!;
        Assert.Equal(2, source.Subtabs.Count);
        Assert.DoesNotContain(source.Subtabs, s => s.DocumentId == "p2");
        Assert.Equal(2, target.Subtabs.Count);
        Assert.Contains(target.Subtabs, s => s.DocumentId == "p2");
    }

    [Fact]
    public void CenterDrop_MergesTwoSingleSubtabNooks_SourceCollapses()
    {
        var layout = new LayoutService();
        string shoreId = layout.CreateShore("main", Leaf("p1", "p1"));
        layout.SplitNook(shoreId, "p1", SplitOrientation.Row, Leaf("p2", "p2"));
        layout.CenterDrop(shoreId, "p2", 0, "p1");
        var root = layout.GetRoot(shoreId)!;
        Assert.IsType<NookLeaf>(root);
        var merged = (NookLeaf)root;
        Assert.Equal("p1", merged.NookId);
        Assert.Equal(2, merged.Subtabs.Count);
        Assert.Contains(merged.Subtabs, s => s.DocumentId == "p2");
    }

    [Fact]
    public void Subtabs_PersistAcrossSnapshotRoundTrip()
    {
        var layout = new LayoutService();
        string shoreId = layout.CreateShore("main", Leaf("p1", "p1", "p2", "p3"));
        var snap = layout.ToSnapshot("ws1", "demo", "/proj");
        var layout2 = new LayoutService();
        layout2.LoadSnapshot(snap);
        var root = layout2.GetRoot(snap.Shores[0].Id)!;
        Assert.IsType<NookLeaf>(root);
        Assert.Equal(3, ((NookLeaf)root).Subtabs.Count);
    }
}
