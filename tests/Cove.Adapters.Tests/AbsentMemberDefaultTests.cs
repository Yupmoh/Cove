using System.Text.Json;
using Cove.Adapters;
using Xunit;

namespace Cove.Adapters.Tests;

public class AbsentMemberDefaultTests
{
    private const string ManifestJson = """
    {
      "name": "demo",
      "displayName": "Demo",
      "description": "d",
      "accent": "#ffffff",
      "binary": "demo",
      "sdkVersion": 2,
      "version": "1.0.0",
      "methods": {},
      "binaryDiscovery": { "versionFlag": "--version" },
      "sessionExtractor": { "script": "x.sh", "schemaVersion": 1, "supportsDepths": ["quick"] }
    }
    """;

    [Fact]
    public void BinaryDiscovery_AbsentCollections_KeepEmpty()
    {
        var m = JsonSerializer.Deserialize(ManifestJson, AdaptersJsonContext.Default.AdapterManifest)!;
        Assert.NotNull(m.BinaryDiscovery);
        Assert.NotNull(m.BinaryDiscovery!.Commands);
        Assert.Empty(m.BinaryDiscovery.Commands);
        Assert.NotNull(m.BinaryDiscovery.WellKnownPaths);
        Assert.Empty(m.BinaryDiscovery.WellKnownPaths);
    }

    [Fact]
    public void SessionExtractor_RequiredMembersDeserialize()
    {
        var m = JsonSerializer.Deserialize(ManifestJson, AdaptersJsonContext.Default.AdapterManifest)!;
        Assert.NotNull(m.SessionExtractor);
        Assert.Equal(1, m.SessionExtractor!.SchemaVersion);
    }

    [Fact]
    public void RegistryEntry_AbsentMembers_KeepDefaults()
    {
        var json = """
        { "adapters": [ { "name": "a", "displayName": "A", "description": "d", "accent": "#fff", "binary": "a", "version": "1.0.0" } ] }
        """;
        var reg = JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.Registry)!;
        Assert.Equal(1, reg.SchemaVersion);
        var e = Assert.Single(reg.Adapters);
        Assert.Equal(1, e.SchemaVersion);
        Assert.NotNull(e.Models);
        Assert.Empty(e.Models);
        Assert.NotNull(e.Platforms);
        Assert.Empty(e.Platforms);
        Assert.NotNull(e.Install);
        Assert.Empty(e.Install);
    }
}
