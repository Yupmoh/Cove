using Cove.Adapters;
using Cove.Engine.Launch;
using Cove.Engine.Restart;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class LaunchOrchestratorProtocolTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-launchproto-" + Guid.NewGuid().ToString("N"));

    private static void WriteAdapter(string root, string name, string binary, string? buildLaunchScript = null, string? detectBinaryScript = null, bool includeBinaryDiscovery = true)
    {
        var dir = Path.Combine(root, name);
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "scripts"));
        var methodEntries = new List<string>();
        if (buildLaunchScript is not null)
            methodEntries.Add($"\"build_launch_command\": {{\"script\": \"{buildLaunchScript}\"}}");
        if (detectBinaryScript is not null)
            methodEntries.Add($"\"detect_binary\": {{\"script\": \"{detectBinaryScript}\"}}");
        var methods = $"\"methods\": {{{string.Join(",", methodEntries)}}}";
        var binaryDiscovery = includeBinaryDiscovery
            ? $",\"binaryDiscovery\": {{\"commands\": [\"{binary}\"], \"wellKnownPaths\": [], \"versionFlag\": \"--version\"}}"
            : "";
        var manifest = $$"""
        {
          "name": "{{name}}",
          "displayName": "Test",
          "description": "test",
          "accent": "#D97757",
          "binary": "{{binary}}",
          "sdkVersion": 2,
          "version": "1.0.0",
          {{methods}}{{binaryDiscovery}}
        }
        """;
        File.WriteAllText(Path.Combine(dir, "adapter.json"), manifest);
    }

    private static void WriteScript(string root, string adapter, string name, string content)
    {
        var path = Path.Combine(root, adapter, name);
        File.WriteAllText(path, "#!/usr/bin/env bash\nset -euo pipefail\n" + content);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite | System.IO.UnixFileMode.UserExecute);
    }

    private static LaunchOrchestrator NewOrchestrator(string root, string? loginShellPath = null)
    {
        var manifestStore = new AdapterManifestStore(root);
        var methodRunner = new MethodRunner();
        var binaryDiscovery = new BinaryDiscoveryService();
        return LaunchTestFactory.Create(
            manifestStore,
            methodRunner,
            binaryDiscovery,
            loginShellPath);
    }

    private static LaunchProfile NewProfile(string adapter = "claude-code") => new(
        Name: "test", Slug: "test", Adapter: adapter, IsDefault: false,
        Model: null, Effort: null,
        CliArgs: Array.Empty<string>(),
        Env: new Dictionary<string, string>(),
        Permissions: new Dictionary<string, bool>(),
        Skills: new List<string>(), Agent: null, SchemaVersion: 1);

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task BuildLaunchCommandAsync_MethodBranch_ParsesCommand_NoFlagDuplication()
    {
        var root = NewDir();
        try
        {
            WriteAdapter(root, "claude-code", "claude", buildLaunchScript: "scripts/build_launch.sh");
            WriteScript(root, "claude-code", "scripts/build_launch.sh",
                "echo '{\"command\":[\"claude\",\"--dangerously-skip-permissions\"]}'");
            var orch = NewOrchestrator(root);
            var profile = NewProfile("claude-code");
            var overrides = new LauncherOverrides { Yolo = true, WorkingDir = "/tmp" };

            var cmd = await orch.BuildLaunchCommandAsync(profile, overrides);

            Assert.Equal("claude", cmd.Command);
            Assert.Contains("--dangerously-skip-permissions", cmd.Args);
            Assert.Equal(1, cmd.Args.Count(a => a == "--dangerously-skip-permissions"));
            Assert.Equal("/tmp", cmd.Cwd);
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task BuildLaunchCommandAsync_MethodBranch_PassesProfileCommandInFlags()
    {
        var root = NewDir();
        try
        {
            WriteAdapter(root, "claude-code", "claude", buildLaunchScript: "scripts/build_launch.sh");
            WriteScript(root, "claude-code", "scripts/build_launch.sh",
                "printf '%s' \"$1\" > \"$(cd \"$(dirname \"$0\")/..\" && pwd)/flags-capture.json\"\necho '{\"command\":[\"ok\"]}'");
            var orch = NewOrchestrator(root);
            var profile = NewProfile("claude-code") with { CliArgs = new[] { "ccx", "--foo" } };
            var overrides = new LauncherOverrides();

            var cmd = await orch.BuildLaunchCommandAsync(profile, overrides);

            Assert.Equal("ok", cmd.Command);
            var flags = File.ReadAllText(Path.Combine(root, "claude-code", "flags-capture.json"));
            Assert.Contains("\"command\":\"ccx\"", flags);
            Assert.Contains("--foo", flags);
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task BuildLaunchCommandAsync_MethodBranch_OmitsCommandWhenProfileHasNoCliArgs()
    {
        var root = NewDir();
        try
        {
            WriteAdapter(root, "claude-code", "claude", buildLaunchScript: "scripts/build_launch.sh");
            WriteScript(root, "claude-code", "scripts/build_launch.sh",
                "printf '%s' \"$1\" > \"$(cd \"$(dirname \"$0\")/..\" && pwd)/flags-capture.json\"\necho '{\"command\":[\"ok\"]}'");
            var orch = NewOrchestrator(root);
            var profile = NewProfile("claude-code");
            var overrides = new LauncherOverrides();

            await orch.BuildLaunchCommandAsync(profile, overrides);

            var flags = File.ReadAllText(Path.Combine(root, "claude-code", "flags-capture.json"));
            Assert.DoesNotContain("\"command\"", flags);
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [Fact]
    public async Task BuildLaunchCommandAsync_UnknownAdapter_FailsFast()
    {
        var root = NewDir();
        try
        {
            var orch = NewOrchestrator(root);
            var profile = NewProfile("nonexistent-adapter");
            var overrides = new LauncherOverrides();

            await Assert.ThrowsAsync<ResumeFailedException>(() => orch.BuildLaunchCommandAsync(profile, overrides));
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task BuildLaunchCommandAsync_MethodExit1_ThrowsCannotBuild()
    {
        var root = NewDir();
        try
        {
            WriteAdapter(root, "failing", "failing-cli", buildLaunchScript: "scripts/build_launch.sh");
            WriteScript(root, "failing", "scripts/build_launch.sh", "echo 'cannot build' >&2; exit 1");
            var orch = NewOrchestrator(root);
            var profile = NewProfile("failing");
            var overrides = new LauncherOverrides();

            await Assert.ThrowsAsync<ResumeFailedException>(() => orch.BuildLaunchCommandAsync(profile, overrides));
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task BuildLaunchCommandAsync_FallbackBranch_ComposesFromBinaryDiscovery()
    {
        var root = NewDir();
        try
        {
            var fakeBinaryPath = Path.Combine(root, "bin");
            Directory.CreateDirectory(fakeBinaryPath);
            var fakeBinary = Path.Combine(fakeBinaryPath, "fake-cli");
            File.WriteAllText(fakeBinary, "#!/usr/bin/env bash\necho '1.0.0'\n");
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(fakeBinary, System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite | System.IO.UnixFileMode.UserExecute);

            WriteAdapter(root, "simple", "fake-cli", includeBinaryDiscovery: true);
            var orch = NewOrchestrator(
                root,
                loginShellPath: fakeBinaryPath + Path.PathSeparator + "/usr/bin:/bin");
            var profile = NewProfile("simple");
            var overrides = new LauncherOverrides { ExtraFlags = new[] { "--verbose" } };

            var cmd = await orch.BuildLaunchCommandAsync(profile, overrides);

            Assert.EndsWith("fake-cli", cmd.Command);
            Assert.Contains("--verbose", cmd.Args);
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task BuildLaunchCommandAsync_FallbackBranch_AppliesYoloWhenNoMethod()
    {
        var root = NewDir();
        try
        {
            var fakeBinaryPath = Path.Combine(root, "bin");
            Directory.CreateDirectory(fakeBinaryPath);
            var fakeBinary = Path.Combine(fakeBinaryPath, "yolo-cli");
            File.WriteAllText(fakeBinary, "#!/usr/bin/env bash\necho '1.0.0'\n");
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(fakeBinary, System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite | System.IO.UnixFileMode.UserExecute);

            WriteAdapter(root, "yolo-adapter", "yolo-cli");
            var orch = NewOrchestrator(
                root,
                loginShellPath: fakeBinaryPath + Path.PathSeparator + "/usr/bin:/bin");
            var profile = NewProfile("yolo-adapter");
            var overrides = new LauncherOverrides { Yolo = true };

            var cmd = await orch.BuildLaunchCommandAsync(profile, overrides);

            Assert.Contains("--dangerously-skip-permissions", cmd.Args);
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task BuildLaunchCommandAsync_V1DetectBinaryFallback_ReturnsPath()
    {
        var root = NewDir();
        try
        {
            var fakeBinaryPath = Path.Combine(root, "bin");
            Directory.CreateDirectory(fakeBinaryPath);
            var fakeBinary = Path.Combine(fakeBinaryPath, "v1-cli");
            File.WriteAllText(fakeBinary, "#!/usr/bin/env bash\nexit 0\n");
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(fakeBinary, System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite | System.IO.UnixFileMode.UserExecute);

            WriteAdapter(root, "v1-adapter", "v1-cli", detectBinaryScript: "scripts/detect_binary.sh", includeBinaryDiscovery: false);
            WriteScript(root, "v1-adapter", "scripts/detect_binary.sh", $"echo '{{\"path\":\"{fakeBinary}\"}}'");
            var orch = NewOrchestrator(root, loginShellPath: fakeBinaryPath);
            var profile = NewProfile("v1-adapter");
            var overrides = new LauncherOverrides();

            var cmd = await orch.BuildLaunchCommandAsync(profile, overrides);

            Assert.Equal(fakeBinary, cmd.Command);
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }
}
