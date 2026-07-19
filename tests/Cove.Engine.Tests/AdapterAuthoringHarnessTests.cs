using System.IO;
using System.Text.Json;
using Cove.Adapters;
using Cove.Engine.Authoring;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class AdapterAuthoringHarnessTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-author-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Scaffold_CreatesAdapterDirectory()
    {
        var root = NewDir();
        Directory.CreateDirectory(root);
        try
        {
            var harness = new AdapterAuthoringHarness(root);
            var dir = harness.Scaffold("my-agent", "My Agent", "A test agent");

            Assert.True(Directory.Exists(dir));
            Assert.True(File.Exists(Path.Combine(dir, "adapter.json")));
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [Fact]
    public void Scaffold_WritesValidManifest()
    {
        var root = NewDir();
        Directory.CreateDirectory(root);
        try
        {
            var harness = new AdapterAuthoringHarness(root);
            var dir = harness.Scaffold("my-agent", "My Agent", "A test agent");

            var manifestPath = Path.Combine(dir, "adapter.json");
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.AdapterManifest);

            Assert.NotNull(manifest);
            Assert.Equal("my-agent", manifest!.Name);
            Assert.Equal("My Agent", manifest.DisplayName);
            Assert.Equal("A test agent", manifest.Description);
            Assert.True(manifest.SdkVersion > 0);
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [Fact]
    public void Scaffold_CreatesScriptsDirectory()
    {
        var root = NewDir();
        Directory.CreateDirectory(root);
        try
        {
            var harness = new AdapterAuthoringHarness(root);
            var dir = harness.Scaffold("my-agent", "My Agent", "A test agent");

            Assert.True(Directory.Exists(Path.Combine(dir, "scripts")));
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [Fact]
    public void Scaffold_CreatesHooksScript()
    {
        var root = NewDir();
        Directory.CreateDirectory(root);
        try
        {
            var harness = new AdapterAuthoringHarness(root);
            var dir = harness.Scaffold("my-agent", "My Agent", "A test agent");

            Assert.True(File.Exists(Path.Combine(dir, "scripts", "hooks.sh")));
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [Fact]
    public void Scaffold_AlreadyExists_Throws()
    {
        var root = NewDir();
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "my-agent"));
        try
        {
            var harness = new AdapterAuthoringHarness(root);
            Assert.Throws<IOException>(() => harness.Scaffold("my-agent", "My Agent", "A test agent"));
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [Fact]
    public void Validate_ManifestIsValid_ReturnsNoErrors()
    {
        var root = NewDir();
        Directory.CreateDirectory(root);
        try
        {
            var harness = new AdapterAuthoringHarness(root);
            var dir = harness.Scaffold("my-agent", "My Agent", "A test agent");
            var errors = harness.Validate(dir);
            Assert.Empty(errors);
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [Fact]
    public void Validate_MissingManifest_ReturnsError()
    {
        var root = NewDir();
        Directory.CreateDirectory(root);
        var dir = Path.Combine(root, "my-agent");
        Directory.CreateDirectory(dir);
        try
        {
            var harness = new AdapterAuthoringHarness(root);
            var errors = harness.Validate(dir);
            Assert.NotEmpty(errors);
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }
}

public sealed class AdapterTestFixtureTests
{
    [Fact]
    public void CreateMinimalManifest_ReturnsValidManifest()
    {
        var manifest = AdapterTestFixture.CreateMinimalManifest("test-adapter");
        Assert.Equal("test-adapter", manifest.Name);
        Assert.Equal("test-adapter", manifest.DisplayName);
        Assert.True(manifest.SdkVersion > 0);
        Assert.NotEmpty(manifest.Methods);
    }

    [Fact]
    public void CreateManifestWithHooks_ReturnsManifestWithHookEnvelopes()
    {
        var manifest = AdapterTestFixture.CreateManifestWithHooks("test-adapter");
        Assert.NotEmpty(manifest.HookEnvelopes);
        Assert.Contains("sessionStartManifest", manifest.HookEnvelopes.Keys);
    }

    [Fact]
    public void WriteManifestToDir_WritesValidJson()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cove-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var manifest = AdapterTestFixture.CreateMinimalManifest("test-adapter");
            AdapterTestFixture.WriteManifestToDir(manifest, dir);

            var path = Path.Combine(dir, "adapter.json");
            Assert.True(File.Exists(path));
            var json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.AdapterManifest);
            Assert.NotNull(parsed);
            Assert.Equal("test-adapter", parsed!.Name);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }
}
