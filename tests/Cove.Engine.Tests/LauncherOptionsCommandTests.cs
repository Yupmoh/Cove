using System.Text.Json;
using Cove.Adapters;
using Cove.Engine.Launch;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class LauncherOptionsCommandTests
{
    private static string WriteFixture(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-launcher-cmd-" + Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(root, name);
        Directory.CreateDirectory(dir);
        var src = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..",
            "tests", "fixtures", "adapters", name);
        foreach (var f in Directory.GetFiles(src))
            File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true);
        return root;
    }

    [Fact]
    public async Task LauncherOptions_Dispatch_ReturnsOptionsArray()
    {
        var root = WriteFixture("test-v2");
        var store = new AdapterManifestStore(root);
        var orch = new LaunchOrchestrator(store, new MethodRunner(), new BinaryDiscoveryService());
        var prm = JsonSerializer.SerializeToElement(new LauncherOptionsParams("test-v2"), CoveJsonContext.Default.LauncherOptionsParams);

        var request = new ControlRequest("1", "cove://commands/launcher.options", prm);
        var response = await EngineCommandRouter.RouteAsync(request, launcher: orch);

        Assert.NotNull(response);
        Assert.True(response!.Ok);
        var options = response.Data!.Value.GetProperty("options");
        Assert.Equal(2, options.GetArrayLength());
        var first = options[0];
        Assert.Equal("model", first.GetProperty("key").GetString());
        Assert.Equal("select", first.GetProperty("type").GetString());
    }

    [Fact]
    public async Task LauncherOptions_Dispatch_NoLauncher_ReturnsNotReady()
    {
        var request = new ControlRequest("1", "cove://commands/launcher.options",
            JsonSerializer.SerializeToElement(new LauncherOptionsParams("test-v2"), CoveJsonContext.Default.LauncherOptionsParams));
        var response = await EngineCommandRouter.RouteAsync(request);

        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("not_ready", response.Error!.Code);
    }

    [Fact]
    public async Task LauncherOptions_Dispatch_AdapterWithoutMethod_ReturnsNotFound()
    {
        var root = WriteFixture("test-v1");
        var store = new AdapterManifestStore(root);
        var orch = new LaunchOrchestrator(store, new MethodRunner(), new BinaryDiscoveryService());
        var prm = JsonSerializer.SerializeToElement(new LauncherOptionsParams("test-v1"), CoveJsonContext.Default.LauncherOptionsParams);

        var request = new ControlRequest("1", "cove://commands/launcher.options", prm);
        var response = await EngineCommandRouter.RouteAsync(request, launcher: orch);

        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("not_found", response.Error!.Code);
    }

    [Fact]
    public async Task LauncherOptions_Dispatch_NoParams_ReturnsInvalidParams()
    {
        var root = WriteFixture("test-v2");
        var store = new AdapterManifestStore(root);
        var orch = new LaunchOrchestrator(store, new MethodRunner(), new BinaryDiscoveryService());

        var request = new ControlRequest("1", "cove://commands/launcher.options");
        var response = await EngineCommandRouter.RouteAsync(request, launcher: orch);

        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("invalid_params", response.Error!.Code);
    }
}
