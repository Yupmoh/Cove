using Cove.Engine.Layout;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SubtabPromoteTests
{
    private static PaneLeaf Leaf(string id, params string[] docIds) => new()
    {
        PaneId = id,
        Subtabs = docIds.Length == 0 ? new[] { new Subtab(id, PaneType.Terminal) } : System.Array.ConvertAll(docIds, d => new Subtab(d, PaneType.Terminal)),
    };

    [Fact]
    public void Promote_RemovesSubtabFromSource_SplitsIntoNewPane()
    {
        var layout = new LayoutService();
        string roomId = layout.CreateRoom("main", Leaf("p1", "p1", "p2", "p3"));
        layout.PromoteSubtab(roomId, "p1", 1, "newPane");
        var root = layout.GetRoot(roomId)!;
        Assert.IsType<SplitNode>(root);
        var split = (SplitNode)root;
        var source = split.ChildA is PaneLeaf la && la.PaneId == "p1" ? la : (split.ChildB as PaneLeaf)!;
        var promoted = split.ChildA is PaneLeaf lb && lb.PaneId == "newPane" ? lb : (split.ChildB as PaneLeaf)!;
        Assert.Equal(2, source.Subtabs.Count);
        Assert.DoesNotContain(source.Subtabs, s => s.DocumentId == "p2");
        Assert.Single(promoted.Subtabs);
        Assert.Equal("p2", promoted.Subtabs[0].DocumentId);
    }

    [Fact]
    public void Promote_SingleSubtab_Fails()
    {
        var layout = new LayoutService();
        string roomId = layout.CreateRoom("main", Leaf("p1", "p1"));
        Assert.Throws<System.InvalidOperationException>(() => layout.PromoteSubtab(roomId, "p1", 0, "newPane"));
    }

    [Fact]
    public void CenterDrop_MovesSubtabBetweenLeaves()
    {
        var layout = new LayoutService();
        string roomId = layout.CreateRoom("main", Leaf("p1", "p1", "p2", "p3"));
        layout.SplitPane(roomId, "p1", SplitOrientation.Row, Leaf("p2", "p2"));
        var beforeSplit = (SplitNode)layout.GetRoot(roomId)!;
        var targetBefore = beforeSplit.ChildA is PaneLeaf la && la.PaneId == "p2" ? la
            : beforeSplit.ChildB is PaneLeaf lb && lb.PaneId == "p2" ? lb
            : throw new System.Exception("p2 leaf not found");
        Assert.Single(targetBefore.Subtabs);

        layout.CenterDrop(roomId, "p1", 1, "p2");

        var root = layout.GetRoot(roomId)!;
        var split = (SplitNode)root;
        var source = split.ChildA is PaneLeaf sa && sa.PaneId == "p1" ? sa : (split.ChildB as PaneLeaf)!;
        var target = split.ChildA is PaneLeaf sb && sb.PaneId == "p2" ? sb : (split.ChildB as PaneLeaf)!;
        Assert.Equal(2, source.Subtabs.Count);
        Assert.DoesNotContain(source.Subtabs, s => s.DocumentId == "p2");
        Assert.Equal(2, target.Subtabs.Count);
        Assert.Contains(target.Subtabs, s => s.DocumentId == "p2");
    }

    [Fact]
    public void CenterDrop_MergesTwoSingleSubtabPanes_SourceCollapses()
    {
        var layout = new LayoutService();
        string roomId = layout.CreateRoom("main", Leaf("p1", "p1"));
        layout.SplitPane(roomId, "p1", SplitOrientation.Row, Leaf("p2", "p2"));
        layout.CenterDrop(roomId, "p2", 0, "p1");
        var root = layout.GetRoot(roomId)!;
        Assert.IsType<PaneLeaf>(root);
        var merged = (PaneLeaf)root;
        Assert.Equal("p1", merged.PaneId);
        Assert.Equal(2, merged.Subtabs.Count);
        Assert.Contains(merged.Subtabs, s => s.DocumentId == "p2");
    }

    [Fact]
    public void Subtabs_PersistAcrossSnapshotRoundTrip()
    {
        var layout = new LayoutService();
        string roomId = layout.CreateRoom("main", Leaf("p1", "p1", "p2", "p3"));
        var snap = layout.ToSnapshot("ws1", "demo", "/proj");
        var layout2 = new LayoutService();
        layout2.LoadSnapshot(snap);
        var root = layout2.GetRoot(snap.Rooms[0].Id)!;
        Assert.IsType<PaneLeaf>(root);
        Assert.Equal(3, ((PaneLeaf)root).Subtabs.Count);
    }
}
