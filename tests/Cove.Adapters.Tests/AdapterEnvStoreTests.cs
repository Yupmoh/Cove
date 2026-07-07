using Cove.Adapters;
using Cove.Protocol;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class AdapterEnvStoreTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-envstore-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var dir = NewDir();
        try
        {
            var store = new AdapterEnvStore(dir);
            var entries = new List<AdapterEnvVar>
            {
                new("API_KEY", "secret", true, "id1"),
                new("DEBUG", "true", false, "id2"),
            };
            store.Save("claude-code", entries);

            var loaded = store.Load("claude-code");
            Assert.Equal(2, loaded.Count);
            Assert.Equal("API_KEY", loaded[0].Key);
            Assert.Equal("secret", loaded[0].Value);
            Assert.True(loaded[0].Enabled);
            Assert.False(loaded[1].Enabled);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Load_NoAdapter_ReturnsEmpty()
    {
        var dir = NewDir();
        try
        {
            var store = new AdapterEnvStore(dir);
            Assert.Empty(store.Load("claude-code"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void ListAdapters_ReturnsAllAdapters()
    {
        var dir = NewDir();
        try
        {
            var store = new AdapterEnvStore(dir);
            store.Save("claude-code", new List<AdapterEnvVar> { new("A", "1") });
            store.Save("codex", new List<AdapterEnvVar> { new("B", "2") });

            var adapters = store.ListAdapters();
            Assert.Equal(2, adapters.Count);
            Assert.Contains("claude-code", adapters);
            Assert.Contains("codex", adapters);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Delete_RemovesAdapter()
    {
        var dir = NewDir();
        try
        {
            var store = new AdapterEnvStore(dir);
            store.Save("claude-code", new List<AdapterEnvVar> { new("A", "1") });
            store.Delete("claude-code");
            Assert.Empty(store.Load("claude-code"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
