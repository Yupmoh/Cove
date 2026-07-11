using Cove.Engine.Launch;
using Cove.Engine.Restart;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class LauncherOverridePersistenceTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-overridepersist-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void PersistOverrides_WritesToDisk()
    {
        var dir = NewDir();
        try
        {
            var store = new LauncherOverrideStore(dir);
            var orch = new LaunchOrchestrator(overrideStore: store);
            var overrides = new LauncherOverrides { Yolo = true, ExtraFlags = new[] { "--verbose" }, WorkingDir = "/tmp" };

            orch.PersistOverrides("nook-1", overrides);

            var fresh = new LauncherOverrideStore(dir);
            Assert.True(fresh.TryLoad("nook-1", out var loaded));
            Assert.True(loaded!.Yolo);
            Assert.Contains("--verbose", loaded.ExtraFlags);
            Assert.Equal("/tmp", loaded.WorkingDir);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void GetOverrides_ReadsFromDisk_WhenNotInMemory()
    {
        var dir = NewDir();
        try
        {
            var store = new LauncherOverrideStore(dir);
            store.Save("nook-2", new LauncherOverrides { Yolo = false, ExtraFlags = new[] { "--model", "x" } });

            var orch = new LaunchOrchestrator(overrideStore: store);
            var retrieved = orch.GetOverrides("nook-2");

            Assert.NotNull(retrieved);
            Assert.False(retrieved!.Yolo);
            Assert.Contains("--model", retrieved.ExtraFlags);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void ClearOverrides_RemovesFromDisk()
    {
        var dir = NewDir();
        try
        {
            var store = new LauncherOverrideStore(dir);
            var orch = new LaunchOrchestrator(overrideStore: store);
            orch.PersistOverrides("nook-3", new LauncherOverrides { Yolo = true });

            orch.ClearOverrides("nook-3");

            var fresh = new LauncherOverrideStore(dir);
            Assert.False(fresh.TryLoad("nook-3", out _));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void LoadAll_ReturnsEveryPersistedOverride()
    {
        var dir = NewDir();
        try
        {
            var store = new LauncherOverrideStore(dir);
            store.Save("nook-a", new LauncherOverrides { Yolo = true });
            store.Save("nook-b", new LauncherOverrides { Yolo = false });

            var all = store.LoadAll();

            Assert.Equal(2, all.Count);
            Assert.Contains(all, kv => kv.Key == "nook-a" && kv.Value.Yolo);
            Assert.Contains(all, kv => kv.Key == "nook-b" && !kv.Value.Yolo);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void TryLoad_UnknownNook_ReturnsFalse()
    {
        var dir = NewDir();
        try
        {
            var store = new LauncherOverrideStore(dir);
            Assert.False(store.TryLoad("never-seen", out _));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Save_RejectsPathTraversalNookId()
    {
        var dir = NewDir();
        try
        {
            var store = new LauncherOverrideStore(dir);
            store.Save("../evil", new LauncherOverrides { Yolo = true });

            Assert.False(store.TryLoad("../evil", out _));
            var evilPath = Path.Combine(Path.GetDirectoryName(dir)!, "evil.json");
            Assert.False(File.Exists(evilPath));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
