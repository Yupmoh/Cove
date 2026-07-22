using Cove.Adapters;
using Cove.Engine.Adapters;
using Cove.Engine.Launch;
using Cove.Protocol;
using Xunit;
using Cove.Testing;

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

    private static string WriteNpmBinary(
        string root,
        string binary,
        string version,
        string package)
    {
        var targetDirectory = Path.Combine(
            [root, "lib", "node_modules", .. package.Split('/'), "bin"]);
        Directory.CreateDirectory(targetDirectory);
        var target = Path.Combine(targetDirectory, binary + ".js");
        File.WriteAllText(target, "#!/bin/sh\necho " + version + "\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                target,
                UnixFileMode.UserRead
                    | UnixFileMode.UserWrite
                    | UnixFileMode.UserExecute);
        }
        var binDirectory = Path.Combine(root, "bin");
        Directory.CreateDirectory(binDirectory);
        File.CreateSymbolicLink(Path.Combine(binDirectory, binary), target);
        return binDirectory;
    }

    private static void WriteAdapter(
        string root,
        string name,
        string binary,
        string binDir,
        string? npmPackage = null)
    {
        var packageIdentity = npmPackage is null
            ? ""
            : $$"""
              "packageIdentity": { "npm": "{{npmPackage}}" },
            """;
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
          {{packageIdentity}}
          "binaryDiscovery": {
            "commands": ["{{binary}}"],
            "wellKnownPaths": ["{{binDir}}"],
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
            new TestHarnessRegistryClient((pkg, _) => { fetched.Add(pkg); return Task.FromResult(latestByPackage(pkg)); }),
            cacheTtl: TimeSpan.Zero));
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task UpdatesCheck_ReportsNewerUpstreamVersion()
    {
        var root = MakeRoot();
        var binDir = WriteNpmBinary(
            root,
            "cove-fake-codex",
            "0.0.1",
            "@openai/codex");
        WriteAdapter(
            root,
            "codex",
            "cove-fake-codex",
            binDir,
            "@openai/codex");
        var store = new AdapterManifestStore(root);
        var orch = LaunchTestFactory.Create(store, new MethodRunner(), new BinaryDiscoveryService());
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

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task UpdatesCheck_ExcludesUpToDateHarness()
    {
        var root = MakeRoot();
        var binDir = WriteNpmBinary(
            root,
            "cove-fake-codex",
            "0.0.1",
            "@openai/codex");
        WriteAdapter(
            root,
            "codex",
            "cove-fake-codex",
            binDir,
            "@openai/codex");
        var store = new AdapterManifestStore(root);
        var orch = LaunchTestFactory.Create(store, new MethodRunner(), new BinaryDiscoveryService());
        ConfigureChecker(_ => "0.0.1", []);

        var request = new ControlRequest("1", "cove://commands/adapter.updates-check");
        var response = await EngineCommandRouter.RouteAsync(request, manifestStore: store, launcher: orch);

        Assert.True(response!.Ok);
        Assert.Equal(0, response.Data!.Value.GetProperty("updates").GetArrayLength());
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task UpdatesCheck_SkipsUnknownAndUndetectedAdapters()
    {
        var root = MakeRoot();
        var binDir = WriteFakeBinary(root, "cove-fake-tool", "0.0.1");
        WriteAdapter(
            root,
            "mystery-tool",
            "cove-fake-tool",
            binDir,
            "mystery-package");
        WriteAdapter(
            root,
            "opencode",
            "cove-absent-binary",
            binDir,
            "opencode-ai");
        var store = new AdapterManifestStore(root);
        var orch = LaunchTestFactory.Create(store, new MethodRunner(), new BinaryDiscoveryService());
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

    private sealed class TestHarnessRegistryClient(
        Func<string, CancellationToken, Task<string?>> fetch) : IHarnessRegistryClient
    {
        public Task<string?> GetLatestVersionAsync(string package, CancellationToken cancellationToken = default)
            => fetch(package, cancellationToken);
    }
}
