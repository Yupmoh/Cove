using Cove.Adapters;
using Cove.Protocol;
using Cove.Testing;
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
        finally { TestDirectory.Delete(dir); }
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
        finally { TestDirectory.Delete(dir); }
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
        finally { TestDirectory.Delete(dir); }
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
        finally { TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Save_FiresEnvSavedEvent_WithAdapter()
    {
        var dir = NewDir();
        try
        {
            var store = new AdapterEnvStore(dir);
            string? firedAdapter = null;
            store.EnvSaved += adapter => firedAdapter = adapter;

            store.Save("claude-code", new List<AdapterEnvVar> { new("A", "1") });

            Assert.Equal("claude-code", firedAdapter);
        }
        finally { TestDirectory.Delete(dir); }
    }

    [Fact]
    public void EnvSaved_DoesNotFire_WhenAdapterInvalid()
    {
        var dir = NewDir();
        try
        {
            var store = new AdapterEnvStore(dir);
            int fireCount = 0;
            store.EnvSaved += _ => fireCount++;

            store.Save("Bad_Adapter!", new List<AdapterEnvVar> { new("A", "1") });

            Assert.Equal(0, fireCount);
        }
        finally { TestDirectory.Delete(dir); }
    }
}
