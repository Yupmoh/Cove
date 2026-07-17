using Cove.Adapters;
using Cove.Engine.Adapters;
using Cove.Engine.Launch;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class AdapterUpdatesCheckCommandTests
{
    private static string MakeRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-updchk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string WriteFakeBinary(string root, string binary, string version)
    {
        var binDir = Path.Combine(root, "bin");
        Directory.CreateDirectory(binDir);
        var bin = Path.Combine(binDir, binary);
        File.WriteAllText(bin, "#!/bin/sh\necho " + version + "\n");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(bin, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return binDir;
    }

    private static void WriteAdapter(string root, string name, string binary, string binDir)
    {
        var dir = Path.Combine(root, name);
        Directory.CreateDirectory(dir);
        var manifest = $$"""
        {
          "sdkVersion": 2,
          "name": "{{name}}",
          "displayName": "{{name}}",
          "description": "test",
          "accent": "#ffffff",
          "binary": "{{binary}}",
          "version": "1.0.0",
          "author": "test",
          "wellKnownPaths": ["{{binDir}}"],
          "binaryDiscovery": {
            "commands": ["{{binary}}"],
            "versionFlag": "--version",
            "versionRegex": "(\\d+\\.\\d+\\.\\d+)"
          },
          "methods": {}
        }
        """;
        File.WriteAllText(Path.Combine(dir, "adapter.json"), manifest);
    }

    private static void ConfigureChecker(Func<string, string?> latestByPackage, List<string> fetched)
    {
        AdapterUpdateCommands.Configure(new HarnessUpdateChecker(
            (pkg, _) => { fetched.Add(pkg); return Task.FromResult(latestByPackage(pkg)); },
            cacheTtl: TimeSpan.Zero));
    }

    [Fact]
    public async Task UpdatesCheck_ReportsNewerUpstreamVersion()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = MakeRoot();
        var binDir = WriteFakeBinary(root, "cove-fake-codex", "0.0.1");
        WriteAdapter(root, "codex", "cove-fake-codex", binDir);
        var store = new AdapterManifestStore(root);
        var orch = new LaunchOrchestrator(store, new MethodRunner(), new BinaryDiscoveryService());
        var fetched = new List<string>();
        ConfigureChecker(_ => "9.9.9", fetched);

        var request = new ControlRequest("1", "cove://commands/adapter.updates-check");
        var response = await EngineCommandRouter.RouteAsync(request, manifestStore: store, launcher: orch);

        Assert.NotNull(response);
        Assert.True(response!.Ok);
        var updates = response.Data!.Value.GetProperty("updates");
        Assert.Equal(1, updates.GetArrayLength());
        var first = updates[0];
        Assert.Equal("codex", first.GetProperty("name").GetString());
        Assert.Equal("0.0.1", first.GetProperty("installedVersion").GetString());
        Assert.Equal("9.9.9", first.GetProperty("latestVersion").GetString());
        Assert.Equal("npm install -g --allow-scripts=@openai/codex @openai/codex@latest", first.GetProperty("updateCommand").GetString());
        Assert.Equal(new[] { "@openai/codex" }, fetched);
    }

    [Fact]
    public async Task UpdatesCheck_ExcludesUpToDateHarness()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = MakeRoot();
        var binDir = WriteFakeBinary(root, "cove-fake-codex", "0.0.1");
        WriteAdapter(root, "codex", "cove-fake-codex", binDir);
        var store = new AdapterManifestStore(root);
        var orch = new LaunchOrchestrator(store, new MethodRunner(), new BinaryDiscoveryService());
        ConfigureChecker(_ => "0.0.1", []);

        var request = new ControlRequest("1", "cove://commands/adapter.updates-check");
        var response = await EngineCommandRouter.RouteAsync(request, manifestStore: store, launcher: orch);

        Assert.True(response!.Ok);
        Assert.Equal(0, response.Data!.Value.GetProperty("updates").GetArrayLength());
    }

    [Fact]
    public async Task UpdatesCheck_SkipsUnknownAndUndetectedAdapters()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = MakeRoot();
        var binDir = WriteFakeBinary(root, "cove-fake-tool", "0.0.1");
        WriteAdapter(root, "mystery-tool", "cove-fake-tool", binDir);
        WriteAdapter(root, "opencode", "cove-absent-binary", binDir);
        var store = new AdapterManifestStore(root);
        var orch = new LaunchOrchestrator(store, new MethodRunner(), new BinaryDiscoveryService());
        var fetched = new List<string>();
        ConfigureChecker(_ => "9.9.9", fetched);

        var request = new ControlRequest("1", "cove://commands/adapter.updates-check");
        var response = await EngineCommandRouter.RouteAsync(request, manifestStore: store, launcher: orch);

        Assert.True(response!.Ok);
        Assert.Equal(0, response.Data!.Value.GetProperty("updates").GetArrayLength());
        Assert.Empty(fetched);
    }

    [Fact]
    public async Task UpdatesCheck_FailsWithoutLauncher()
    {
        var root = MakeRoot();
        var store = new AdapterManifestStore(root);

        var request = new ControlRequest("1", "cove://commands/adapter.updates-check");
        var response = await EngineCommandRouter.RouteAsync(request, manifestStore: store);

        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("not_ready", response.Error!.Code);
    }
}
