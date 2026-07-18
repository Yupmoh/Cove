using Cove.Engine.Layout;
using Cove.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class LayoutWingPinFocusTests
{
    private static NookLeaf Leaf(string id) =>
        new() { NookId = id, Subtabs = new[] { new Subtab(id, NookType.Terminal) } };

    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-wpf-" + System.Guid.NewGuid().ToString("N"));

    [Fact]
    public void NewShore_DefaultsToMainWing_Unpinned()
    {
        var layout = new LayoutService();
        var shoreId = layout.CreateShore("main", Leaf("p1"));
        var view = layout.ShoresFor(LayoutService.DefaultBayId).Single(s => s.Id == shoreId);
        Assert.Equal(LayoutService.MainWingId, view.WingId);
        Assert.False(view.Pinned);
        var wings = layout.WingsFor(LayoutService.DefaultBayId);
        Assert.Single(wings);
        Assert.Equal(LayoutService.MainWingId, wings[0].Id);
    }

    [Fact]
    public void WingPinFocus_RoundTripThroughSnapshot()
    {
        var layout = new LayoutService();
        var a = layout.CreateShore("a", Leaf("p1"));
        var b = layout.CreateShore("b", Leaf("p2"));
        var wingId = layout.CreateWing(LayoutService.DefaultBayId, "side");
        layout.MoveShoreToWing(LayoutService.DefaultBayId, b, wingId);
        layout.SetShorePinned(LayoutService.DefaultBayId, a, true);
        layout.SetWingIcon(LayoutService.DefaultBayId, wingId, "emoji", "star");
        layout.SetFocusedNook(LayoutService.DefaultBayId, "p2");

        var snap = layout.ToSnapshot(LayoutService.DefaultBayId, "demo", "/proj");
        Assert.Equal("p2", snap.FocusedNookId);
        Assert.Equal(2, snap.Wings.Count);
        Assert.Contains(snap.Shores, s => s.Id == a && s.Pinned && s.WingId == LayoutService.MainWingId);
        Assert.Contains(snap.Shores, s => s.Id == b && !s.Pinned && s.WingId == wingId);
        var sideWing = snap.Wings.Single(w => w.Id == wingId);
        Assert.Equal("side", sideWing.Name);
        Assert.Equal("emoji", sideWing.IconKind);
        Assert.Equal("star", sideWing.IconValue);

        var reloaded = new LayoutService();
        reloaded.LoadSnapshot(snap);
        var shores = reloaded.ShoresFor(LayoutService.DefaultBayId);
        Assert.True(shores.Single(s => s.Id == a).Pinned);
        Assert.Equal(wingId, shores.Single(s => s.Id == b).WingId);
        Assert.Equal("p2", reloaded.FocusedNookFor(LayoutService.DefaultBayId));
        Assert.Equal(2, reloaded.WingsFor(LayoutService.DefaultBayId).Count);
    }

    [Fact]
    public void LegacySnapshot_WithoutWings_DefaultsToMainWing()
    {
        var legacy = new BaySnapshot
        {
            Id = LayoutService.DefaultBayId,
            Name = "demo",
            ProjectDir = "/proj",
            Shores = new[]
            {
                new ShoreSnapshot { Id = "r1", Name = "main", LayoutTree = Leaf("p1") },
            },
        };
        var layout = new LayoutService();
        layout.LoadSnapshot(legacy);
        var wings = layout.WingsFor(LayoutService.DefaultBayId);
        Assert.Single(wings);
        Assert.Equal(LayoutService.MainWingId, wings[0].Id);
        var shore = layout.ShoresFor(LayoutService.DefaultBayId).Single();
        Assert.Equal(LayoutService.MainWingId, shore.WingId);
        Assert.False(shore.Pinned);
    }

    [Fact]
    public void WingPinFocus_PersistThroughSaveLoad()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var layout = new LayoutService();
            var a = layout.CreateShore("a", Leaf("p1"));
            var b = layout.CreateShore("b", Leaf("p2"));
            var wingId = layout.CreateWing(LayoutService.DefaultBayId, "side");
            layout.MoveShoreToWing(LayoutService.DefaultBayId, b, wingId);
            layout.SetShorePinned(LayoutService.DefaultBayId, a, true);
            layout.SetFocusedNook(LayoutService.DefaultBayId, "p1");
            BayPersistence.Save(layout.ToSnapshot(LayoutService.DefaultBayId, "demo", "/proj"), new NookDescriptor[0], dir);

            var (loaded, _) = BayPersistence.Load(dir, NullLogger.Instance);
            Assert.NotNull(loaded);
            var reloaded = new LayoutService();
            reloaded.LoadSnapshot(loaded!);
            Assert.True(reloaded.ShoresFor(LayoutService.DefaultBayId).Single(s => s.Id == a).Pinned);
            Assert.Equal(wingId, reloaded.ShoresFor(LayoutService.DefaultBayId).Single(s => s.Id == b).WingId);
            Assert.Equal("p1", reloaded.FocusedNookFor(LayoutService.DefaultBayId));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void RemoveWing_RehomesShoresToMain()
    {
        var layout = new LayoutService();
        var s = layout.CreateShore("a", Leaf("p1"));
        var wingId = layout.CreateWing(LayoutService.DefaultBayId, "side");
        layout.MoveShoreToWing(LayoutService.DefaultBayId, s, wingId);
        layout.RemoveWing(LayoutService.DefaultBayId, wingId);
        Assert.Single(layout.WingsFor(LayoutService.DefaultBayId));
        Assert.Equal(LayoutService.MainWingId, layout.ShoresFor(LayoutService.DefaultBayId).Single().WingId);
    }
}
