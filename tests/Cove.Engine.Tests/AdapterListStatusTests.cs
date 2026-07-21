using Cove.Adapters;
using Cove.Engine.Launch;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class AdapterListStatusTests
{
    private static string WriteFixture(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-adapterlist-" + Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(root, name);
        Directory.CreateDirectory(dir);
        var src = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..",
            "tests", "fixtures", "adapters", name);
        foreach (var f in Directory.GetFiles(src))
            File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true);
        return root;
    }

    [Fact]
    public async Task AdapterList_WithLauncher_IncludesStatus()
    {
        var root = WriteFixture("test-v2");
        File.WriteAllText(Path.Combine(root, "test-v2", "icon.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M0 0h1v1z\"/></svg>");
        var store = new AdapterManifestStore(root);
        var orch = LaunchTestFactory.Create(store, new MethodRunner(), new BinaryDiscoveryService());

        var request = new ControlRequest("1", "cove://commands/adapter.list");
        var response = await EngineCommandRouter.RouteAsync(request, manifestStore: store, launcher: orch);

        Assert.NotNull(response);
        Assert.True(response!.Ok);
        var adapters = response.Data!.Value.GetProperty("adapters");
        Assert.Equal(1, adapters.GetArrayLength());
        var first = adapters[0];
        Assert.True(first.TryGetProperty("status", out var status));
        Assert.Equal("missing", status.GetString());
        Assert.Contains("<svg", first.GetProperty("iconSvg").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdapterList_NoLauncher_Succeeds_WithoutStatus()
    {
        var root = WriteFixture("test-v2");
        var store = new AdapterManifestStore(root);

        var request = new ControlRequest("1", "cove://commands/adapter.list");
        var response = await EngineCommandRouter.RouteAsync(request, manifestStore: store);

        Assert.NotNull(response);
        Assert.True(response!.Ok);
        var adapters = response.Data!.Value.GetProperty("adapters");
        Assert.Equal(1, adapters.GetArrayLength());
        Assert.False(adapters[0].TryGetProperty("status", out _));
    }
}
