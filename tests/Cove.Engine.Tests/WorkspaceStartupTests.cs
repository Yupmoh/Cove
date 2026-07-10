using System.IO;
using System.Linq;
using Cove.Engine.Layout;
using Cove.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class WorkspaceStartupTests
{
    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "cove-startup-" + System.Guid.NewGuid().ToString("N"));

    private static void WriteWs(string root, string id, string name, string projectDir)
    {
        var dir = Path.Combine(root, id);
        var snap = new WorkspaceSnapshot
        {
            Id = id,
            Name = name,
            ProjectDir = projectDir,
            Rooms = new[] { new RoomSnapshot { Id = "r-" + id, Name = "Terminal 1", LayoutTree = new PaneLeaf { PaneId = "p-" + id, Subtabs = new[] { new Subtab("p-" + id, PaneType.Terminal) } } } },
        };
        WorkspacePersistence.Save(snap, new PaneDescriptor[0], dir);
    }

    [Fact]
    public void Enumerate_LoadsAllPersistedWorkspaces()
    {
        var root = NewRoot();
        try
        {
            WriteWs(root, "wsA", "Alpha", "/tmp/a");
            WriteWs(root, "wsB", "Beta", "/tmp/b");
            var loaded = WorkspaceStartup.Enumerate(root, NullLogger.Instance);
            Assert.Equal(2, loaded.Count);
            Assert.Contains(loaded, l => l.Snapshot.Id == "wsA");
            Assert.Contains(loaded, l => l.Snapshot.Id == "wsB");
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void Enumerate_MissingRoot_ReturnsEmpty()
    {
        var loaded = WorkspaceStartup.Enumerate(Path.Combine(NewRoot(), "nope"), NullLogger.Instance);
        Assert.Empty(loaded);
    }

    [Fact]
    public void DisplayName_LegacyDefault_DerivesFromProjectDir()
    {
        var snap = new WorkspaceSnapshot { Id = "default", Name = "default", ProjectDir = "/Users/moh/code/cove" };
        Assert.Equal("cove", WorkspaceStartup.DisplayName(snap, "/fallback"));
    }

    [Fact]
    public void DisplayName_NamedWorkspace_KeepsName()
    {
        var snap = new WorkspaceSnapshot { Id = "x", Name = "My Project", ProjectDir = "/tmp/p" };
        Assert.Equal("My Project", WorkspaceStartup.DisplayName(snap, "/fallback"));
    }

    [Fact]
    public void Migration_LegacyDefault_AdoptedIntoManagerAndLayout()
    {
        var root = NewRoot();
        try
        {
            WriteWs(root, "default", "default", "/Users/moh/code/cove");
            var loaded = WorkspaceStartup.Enumerate(root, NullLogger.Instance);
            Assert.Single(loaded);

            var layout = new LayoutService();
            foreach (var entry in loaded)
                layout.LoadSnapshot(entry.Snapshot);

            Assert.Contains("default", layout.WorkspaceIds);
            var snap = layout.ToSnapshot("default", "default", "/x");
            Assert.Single(snap.Rooms);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
