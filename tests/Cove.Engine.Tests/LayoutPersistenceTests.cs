using System.IO;
using Cove.Engine.Layout;
using Cove.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class LayoutPersistenceTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-persist-" + System.Guid.NewGuid().ToString("N"));

    [Fact]
    public void MosaicMutation_PersistsBayJson()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var layout = new LayoutService();
            string shoreId = layout.CreateShore("main", new NookLeaf { NookId = "p1", Subtabs = new[] { new Subtab("p1", NookType.Terminal) } });
            layout.SplitNook(shoreId, "p1", SplitOrientation.Row, new NookLeaf { NookId = "p2", Subtabs = new[] { new Subtab("p2", NookType.Terminal) } });

            var snap = layout.ToSnapshot("ws1", "demo", "/proj");
            BayPersistence.Save(snap, new NookDescriptor[0], dir);

            Assert.True(File.Exists(Path.Combine(dir, "bay.json")));
            var (loaded, _) = BayPersistence.Load(dir, NullLogger.Instance);
            Assert.NotNull(loaded);
            Assert.IsType<SplitNode>(loaded!.Shores[0].LayoutTree);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void CorruptedBayJson_FallsBackToBak()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var layout = new LayoutService();
            string shoreId = layout.CreateShore("main", new NookLeaf { NookId = "p1", Subtabs = new[] { new Subtab("p1", NookType.Terminal) } });
            BayPersistence.Save(layout.ToSnapshot("ws1", "demo", "/proj"), new NookDescriptor[0], dir);
            layout.SplitNook(shoreId, "p1", SplitOrientation.Row, new NookLeaf { NookId = "p2", Subtabs = new[] { new Subtab("p2", NookType.Terminal) } });
            BayPersistence.Save(layout.ToSnapshot("ws1", "demo", "/proj"), new NookDescriptor[0], dir);

            File.WriteAllText(Path.Combine(dir, "bay.json"), "{CORRUPT");
            var (loaded, _) = BayPersistence.Load(dir, NullLogger.Instance);
            Assert.NotNull(loaded);
            Assert.Equal("ws1", loaded!.Id);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void ProjectDir_RoundTripsThroughSaveLoad()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var layout = new LayoutService();
            layout.CreateShore("main", new NookLeaf { NookId = "p1", Subtabs = new[] { new Subtab("p1", NookType.Terminal) } });
            var snap = layout.ToSnapshot("ws1", "demo", "/my/project");
            BayPersistence.Save(snap, new NookDescriptor[0], dir);

            var (loaded, _) = BayPersistence.Load(dir, NullLogger.Instance);
            Assert.Equal("/my/project", loaded!.ProjectDir);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void NookTitle_PersistsInSessionJson()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var descs = new[] { new NookDescriptor("p1", "/bin/sh", new[] { "-l" }, "/tmp", "my nook") };
            var layout = new BaySnapshot
            {
                Id = "ws1",
                Name = "demo",
                ProjectDir = "/proj",
                Shores = new[] { new ShoreSnapshot { Id = "r1", Name = "main", LayoutTree = new NookLeaf { NookId = "p1" } } },
            };
            BayPersistence.Save(layout, descs, dir);

            var (_, sessions) = BayPersistence.Load(dir, NullLogger.Instance);
            Assert.True(sessions.ContainsKey("p1"));
            Assert.Equal("my nook", sessions["p1"].Title);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }
}
