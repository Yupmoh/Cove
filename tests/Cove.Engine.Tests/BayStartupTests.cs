using System.IO;
using System.Linq;
using Cove.Engine.Layout;
using Cove.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BayStartupTests
{
    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "cove-startup-" + System.Guid.NewGuid().ToString("N"));

    private static void WriteWs(string root, string id, string name, string projectDir)
    {
        var dir = Path.Combine(root, id);
        var snap = new BaySnapshot
        {
            Id = id,
            Name = name,
            ProjectDir = projectDir,
            Shores = new[] { new ShoreSnapshot { Id = "r-" + id, Name = "Terminal 1", LayoutTree = new NookLeaf { NookId = "p-" + id, Subtabs = new[] { new Subtab("p-" + id, NookType.Terminal) } } } },
        };
        BayPersistence.Save(snap, new NookDescriptor[0], dir);
    }

    [Fact]
    public void Enumerate_LoadsAllPersistedBays()
    {
        var root = NewRoot();
        try
        {
            WriteWs(root, "wsA", "Alpha", "/tmp/a");
            WriteWs(root, "wsB", "Beta", "/tmp/b");
            var loaded = BayStartup.Enumerate(root, NullLogger.Instance);
            Assert.Equal(2, loaded.Count);
            Assert.Contains(loaded, l => l.Snapshot.Id == "wsA");
            Assert.Contains(loaded, l => l.Snapshot.Id == "wsB");
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void Enumerate_MissingRoot_ReturnsEmpty()
    {
        var loaded = BayStartup.Enumerate(Path.Combine(NewRoot(), "nope"), NullLogger.Instance);
        Assert.Empty(loaded);
    }

    [Fact]
    public void DisplayName_LegacyDefault_DerivesFromProjectDir()
    {
        var snap = new BaySnapshot { Id = "default", Name = "default", ProjectDir = "/Users/moh/code/cove" };
        Assert.Equal("cove", BayStartup.DisplayName(snap, "/fallback"));
    }

    [Fact]
    public void DisplayName_NamedBay_KeepsName()
    {
        var snap = new BaySnapshot { Id = "x", Name = "My Project", ProjectDir = "/tmp/p" };
        Assert.Equal("My Project", BayStartup.DisplayName(snap, "/fallback"));
    }

    [Fact]
    public void Migration_LegacyDefault_AdoptedIntoManagerAndLayout()
    {
        var root = NewRoot();
        try
        {
            WriteWs(root, "default", "default", "/Users/moh/code/cove");
            var loaded = BayStartup.Enumerate(root, NullLogger.Instance);
            Assert.Single(loaded);

            var layout = new LayoutService();
            foreach (var entry in loaded)
                layout.LoadSnapshot(entry.Snapshot);

            Assert.Contains("default", layout.BayIds);
            var snap = layout.ToSnapshot("default", "default", "/x");
            Assert.Single(snap.Shores);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
