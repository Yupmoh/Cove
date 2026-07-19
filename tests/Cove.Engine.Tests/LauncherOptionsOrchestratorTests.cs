using System.IO;
using Cove.Adapters;
using Cove.Engine.Launch;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class LauncherOptionsOrchestratorTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-launcher-opts-" + Guid.NewGuid().ToString("N"));

    private static string WriteFixture(string name)
    {
        var root = NewDir();
        var dir = Path.Combine(root, name);
        Directory.CreateDirectory(dir);
        File.Copy(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..",
            "tests", "fixtures", "adapters", name, "adapter.json"), Path.Combine(dir, "adapter.json"));
        foreach (var f in Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..",
            "tests", "fixtures", "adapters", name)))
            if (Path.GetFileName(f) != "adapter.json")
                File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true);
        return root;
    }

    [Fact]
    public async Task LoadLauncherOptionsAsync_StaticFile_ParsesOptions()
    {
        var dir = WriteFixture("test-v2");
        var store = new AdapterManifestStore(dir);
        var runner = new MethodRunner();
        var orch = LaunchTestFactory.Create(store, runner, new BinaryDiscoveryService());
        var options = await orch.LoadLauncherOptionsAsync("test-v2");

        Assert.NotNull(options);
        Assert.Equal(2, options!.Options.Count);
        Assert.Contains(options.Options, o => o.Key == "model" && o.Type == "select");
        Assert.Contains(options.Options, o => o.Key == "verbose" && o.Type == "toggle");
    }

    [Fact]
    public async Task LoadLauncherOptionsAsync_NoMethod_ReturnsNull()
    {
        var dir = WriteFixture("test-v1");
        var store = new AdapterManifestStore(dir);
        var runner = new MethodRunner();
        var orch = LaunchTestFactory.Create(store, runner, new BinaryDiscoveryService());

        var options = await orch.LoadLauncherOptionsAsync("test-v1");

        Assert.Null(options);
    }

    [Fact]
    public async Task LoadLauncherOptionsAsync_UnknownAdapter_ReturnsNull()
    {
        var dir = WriteFixture("test-v2");
        var store = new AdapterManifestStore(dir);
        var runner = new MethodRunner();
        var orch = LaunchTestFactory.Create(store, runner, new BinaryDiscoveryService());

        var options = await orch.LoadLauncherOptionsAsync("never-installed");

        Assert.Null(options);
    }

    [Fact]
    public async Task LoadLauncherOptionsAsync_SelectChoice_ParsesValueLabel()
    {
        var dir = WriteFixture("test-v2");
        var store = new AdapterManifestStore(dir);
        var runner = new MethodRunner();
        var orch = LaunchTestFactory.Create(store, runner, new BinaryDiscoveryService());
        var options = await orch.LoadLauncherOptionsAsync("test-v2");

        Assert.NotNull(options);
        var select = options!.Options.First(o => o.Type == "select");
        Assert.NotNull(select.Choices);
        Assert.Equal(2, select.Choices!.Count);
        Assert.Contains(select.Choices, c => c.Value == "default" && c.Label == "Default");
    }

    [Fact]
    public async Task LoadLauncherOptionsAsync_Toggle_HasBoolDefault()
    {
        var dir = WriteFixture("test-v2");
        var store = new AdapterManifestStore(dir);
        var runner = new MethodRunner();
        var orch = LaunchTestFactory.Create(store, runner, new BinaryDiscoveryService());

        var options = await orch.LoadLauncherOptionsAsync("test-v2");

        Assert.NotNull(options);
        var toggle = options!.Options.First(o => o.Type == "toggle");
        Assert.Equal("false", toggle.DefaultValueRaw);
    }
}
