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
    public void MosaicMutation_PersistsWorkspaceJson()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var layout = new LayoutService();
            string roomId = layout.CreateRoom("main", new PaneLeaf { PaneId = "p1", Subtabs = new[] { new Subtab("p1", PaneType.Terminal) } });
            layout.SplitPane(roomId, "p1", SplitOrientation.Row, new PaneLeaf { PaneId = "p2", Subtabs = new[] { new Subtab("p2", PaneType.Terminal) } });

            var snap = layout.ToSnapshot("ws1", "demo", "/proj");
            WorkspacePersistence.Save(snap, new PaneDescriptor[0], dir);

            Assert.True(File.Exists(Path.Combine(dir, "workspace.json")));
            var (loaded, _) = WorkspacePersistence.Load(dir, NullLogger.Instance);
            Assert.NotNull(loaded);
            Assert.IsType<SplitNode>(loaded!.Rooms[0].LayoutTree);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void CorruptedWorkspaceJson_FallsBackToBak()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var layout = new LayoutService();
            string roomId = layout.CreateRoom("main", new PaneLeaf { PaneId = "p1", Subtabs = new[] { new Subtab("p1", PaneType.Terminal) } });
            WorkspacePersistence.Save(layout.ToSnapshot("ws1", "demo", "/proj"), new PaneDescriptor[0], dir);
            layout.SplitPane(roomId, "p1", SplitOrientation.Row, new PaneLeaf { PaneId = "p2", Subtabs = new[] { new Subtab("p2", PaneType.Terminal) } });
            WorkspacePersistence.Save(layout.ToSnapshot("ws1", "demo", "/proj"), new PaneDescriptor[0], dir);

            File.WriteAllText(Path.Combine(dir, "workspace.json"), "{CORRUPT");
            var (loaded, _) = WorkspacePersistence.Load(dir, NullLogger.Instance);
            Assert.NotNull(loaded);
            Assert.Equal("ws1", loaded!.Id);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void ProjectDir_RoundTripsThroughSaveLoad()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var layout = new LayoutService();
            layout.CreateRoom("main", new PaneLeaf { PaneId = "p1", Subtabs = new[] { new Subtab("p1", PaneType.Terminal) } });
            var snap = layout.ToSnapshot("ws1", "demo", "/my/project");
            WorkspacePersistence.Save(snap, new PaneDescriptor[0], dir);

            var (loaded, _) = WorkspacePersistence.Load(dir, NullLogger.Instance);
            Assert.Equal("/my/project", loaded!.ProjectDir);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void PaneTitle_PersistsInSessionJson()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var descs = new[] { new PaneDescriptor("p1", "/bin/sh", new[] { "-l" }, "/tmp", "my pane") };
            var layout = new WorkspaceSnapshot
            {
                Id = "ws1",
                Name = "demo",
                ProjectDir = "/proj",
                Rooms = new[] { new RoomSnapshot { Id = "r1", Name = "main", LayoutTree = new PaneLeaf { PaneId = "p1" } } },
            };
            WorkspacePersistence.Save(layout, descs, dir);

            var (_, sessions) = WorkspacePersistence.Load(dir, NullLogger.Instance);
            Assert.True(sessions.ContainsKey("p1"));
            Assert.Equal("my pane", sessions["p1"].Title);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
