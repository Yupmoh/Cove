using System.Text.Json;
using Cove.Adapters;
using Cove.Engine.Launch;
using Cove.Engine.Restart;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class RealAdapterGateTests
{
    private static string? RealAdaptersRoot()
    {
        var candidates = new[]
        {
            "/tmp/cove-gate1-test/adapters",
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".cove", "adapters"),
        };
        foreach (var c in candidates)
            if (Directory.Exists(Path.Combine(c, "claude-code")))
                return c;
        return null;
    }

    [Fact]
    public async Task RealClaudeCode_Manifest_ParsesAllFields()
    {
        var root = RealAdaptersRoot();
        if (root is null) return;
        var store = new AdapterManifestStore(root);
        var manifest = store.Load("claude-code");
        Assert.NotNull(manifest);
        Assert.Equal("claude-code", manifest!.Name);
        Assert.Equal(2, manifest.SdkVersion);
        Assert.NotEmpty(manifest.Binary);
        Assert.NotEmpty(manifest.Version);
        Assert.NotEmpty(manifest.Hooks);
        Assert.NotEmpty(manifest.BinaryDiscovery?.Commands ?? []);
        Assert.Contains("build_launch_command", manifest.Methods.Keys);
        Assert.Contains("build_resume_command", manifest.Methods.Keys);
    }

    [Fact]
    public async Task RealClaudeCode_BuildLaunchCommand_ManifestProtocol()
    {
        var root = RealAdaptersRoot();
        if (root is null) return;
        var store = new AdapterManifestStore(root);
        var runner = new MethodRunner();
        var orch = new LaunchOrchestrator(store, runner, new BinaryDiscoveryService());
        var profile = new LaunchProfile("claude-code", "default", "claude-code", true, null, null,
            new[] { "claude" }, new System.Collections.Generic.Dictionary<string, string>(),
            new System.Collections.Generic.Dictionary<string, bool>(),
            System.Array.Empty<string>(), null, 2);
        var overrides = new LauncherOverrides();

        var cmd = await orch.BuildLaunchCommandAsync(profile, overrides);

        Assert.NotNull(cmd);
        Assert.False(string.IsNullOrEmpty(cmd.Command));
        Assert.Equal("claude", Path.GetFileNameWithoutExtension(cmd.Command));
    }

    [Fact]
    public async Task RealClaudeCode_BuildResumeCommand_ManifestProtocol()
    {
        var root = RealAdaptersRoot();
        if (root is null) return;
        var store = new AdapterManifestStore(root);
        var runner = new MethodRunner();
        var proto = new AdapterResumeProtocol(store, runner);
        var overrides = new LauncherOverrides { WorkingDir = "/tmp" };

        var cmd = await proto.BuildResumeCommandAsync("claude-code", "gate1-test-session", overrides);

        Assert.NotNull(cmd);
        Assert.False(string.IsNullOrEmpty(cmd.Command));
        Assert.Contains("gate1-test-session", cmd.Args);
    }

    [Fact]
    public async Task RealClaudeCode_LauncherOptions_Parse()
    {
        var root = RealAdaptersRoot();
        if (root is null) return;
        var store = new AdapterManifestStore(root);
        var runner = new MethodRunner();
        var orch = new LaunchOrchestrator(store, runner, new BinaryDiscoveryService());

        var options = await orch.LoadLauncherOptionsAsync("claude-code");

        Assert.NotNull(options);
        Assert.NotEmpty(options!.Options);
    }

    [Fact]
    public void HookEnvelopeKind_SerializesCamelCase_RoundTripsThroughSchema()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "validate-adapter.sh");
        if (!File.Exists(scriptPath)) return;

        var manifest = new AdapterManifest
        {
            SdkVersion = 2,
            Name = "roundtrip-test",
            DisplayName = "Roundtrip",
            Description = "test",
            Accent = "#3b82f6",
            Binary = "rt",
            Version = "1.0.0",
            Methods = new System.Collections.Generic.Dictionary<string, AdapterMethod>(),
            HookEnvelopes = new System.Collections.Generic.Dictionary<string, HookEnvelopeDeclaration>
            {
                ["sessionStartManifest"] = new() { Kind = HookEnvelopeKind.Identity },
                ["userPromptSubmit"] = new() { Kind = HookEnvelopeKind.HookSpecificOutput, IncludeSystemMessage = true },
                ["preToolUse"] = new() { Kind = HookEnvelopeKind.FlatAdditionalContext },
            },
        };
        var json = System.Text.Json.JsonSerializer.Serialize(manifest, Cove.Adapters.AdaptersJsonContext.Default.AdapterManifest);

        Assert.Contains("identity", json);
        Assert.Contains("hookSpecificOutput", json);
        Assert.Contains("flatAdditionalContext", json);
        Assert.DoesNotContain("Identity", json);
        Assert.DoesNotContain("HookSpecificOutput", json);

        var root = Path.Combine(Path.GetTempPath(), "cove-roundtrip-" + Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(root, "roundtrip-test");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "adapter.json"), json);
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = scriptPath,
                Arguments = $"--no-color \"{dir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var proc = System.Diagnostics.Process.Start(psi)!;
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            Assert.True(proc.ExitCode == 0, $"validate-adapter failed: {output}");
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
