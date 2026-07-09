using Cove.Adapters;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class AdapterManifestStoreTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-manifeststore-" + Guid.NewGuid().ToString("N"));

    private static void WriteManifest(string dir, string name = "test-adapter", string binary = "test-cli")
    {
        Directory.CreateDirectory(dir);
        var manifest = $$"""
        {
          "name": "{{name}}",
          "displayName": "Test",
          "description": "test adapter",
          "accent": "#D97757",
          "binary": "{{binary}}",
          "sdkVersion": 2,
          "version": "1.0.0",
          "binaryDiscovery": {"commands": ["{{binary}}"], "wellKnownPaths": []},
          "methods": {
            "build_launch_command": {"script": "build_launch.sh"}
          }
        }
        """;
        File.WriteAllText(Path.Combine(dir, "adapter.json"), manifest);
    }

    [Fact]
    public void Load_ReturnsManifest_WhenExists()
    {
        var root = NewDir();
        try
        {
            var adapterDir = Path.Combine(root, "claude-code");
            WriteManifest(adapterDir, "claude-code", "claude");
            var store = new AdapterManifestStore(root);

            var manifest = store.Load("claude-code");

            Assert.NotNull(manifest);
            Assert.Equal("claude-code", manifest!.Name);
            Assert.Equal("claude", manifest.Binary);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void Load_ReturnsNull_WhenMissing()
    {
        var root = NewDir();
        try
        {
            var store = new AdapterManifestStore(root);

            var manifest = store.Load("nonexistent");

            Assert.Null(manifest);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void Load_ReturnsNull_WhenJsonCorrupt()
    {
        var root = NewDir();
        try
        {
            var adapterDir = Path.Combine(root, "broken");
            Directory.CreateDirectory(adapterDir);
            File.WriteAllText(Path.Combine(adapterDir, "adapter.json"), "{ not valid json");
            var store = new AdapterManifestStore(root);

            var manifest = store.Load("broken");

            Assert.Null(manifest);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void Load_CachesByLastWriteTime()
    {
        var root = NewDir();
        try
        {
            var adapterDir = Path.Combine(root, "cached");
            WriteManifest(adapterDir, "cached", "cli-v1");
            var store = new AdapterManifestStore(root);

            var first = store.Load("cached");
            var second = store.Load("cached");

            Assert.NotNull(first);
            Assert.Same(first, second);

            WriteManifest(adapterDir, "cached", "cli-v2");
            File.SetLastWriteTimeUtc(Path.Combine(adapterDir, "adapter.json"), DateTimeOffset.UtcNow.AddSeconds(1).UtcDateTime);
            var third = store.Load("cached");

            Assert.NotNull(third);
            Assert.NotSame(first, third);
            Assert.Equal("cli-v2", third!.Binary);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void LoadAll_ReturnsEveryValidManifest()
    {
        var root = NewDir();
        try
        {
            WriteManifest(Path.Combine(root, "alpha"), "alpha", "alpha-cli");
            WriteManifest(Path.Combine(root, "beta"), "beta", "beta-cli");
            var store = new AdapterManifestStore(root);

            var manifests = store.LoadAll();

            Assert.Equal(2, manifests.Count);
            Assert.Contains(manifests, m => m.Name == "alpha");
            Assert.Contains(manifests, m => m.Name == "beta");
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void ResolveDir_ReturnsAdapterSubdir()
    {
        var store = new AdapterManifestStore("/fake/root");

        var dir = store.ResolveDir("claude-code");

        Assert.Equal(Path.Combine("/fake/root", "claude-code"), dir);
    }

    [Fact]
    public void Load_RejectsPathTraversal()
    {
        var root = NewDir();
        try
        {
            WriteManifest(Path.Combine(root, "real"), "real", "real-cli");
            var store = new AdapterManifestStore(root);

            var manifest = store.Load("../real");

            Assert.Null(manifest);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
